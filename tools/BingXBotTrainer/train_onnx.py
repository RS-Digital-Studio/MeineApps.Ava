#!/usr/bin/env python3
"""
BingXBot ONNX Model Trainer
============================
Liest gelabelte FeatureSnapshots aus der SQLite-DB und trainiert ein Modell
das als .onnx exportiert wird. Die .onnx Datei kann von OnnxModelInference (C#) geladen werden.

Modelle:
  1. LightGBM (schnell, robust, wenig Overfitting) - DEFAULT
  2. MLP (Multi-Layer Perceptron, für mehr Kapazität)
  3. Transformer (experimentell, braucht viele Daten)

Verwendung:
  pip install -r requirements.txt
  python train_onnx.py                          # Standard: LightGBM
  python train_onnx.py --model mlp              # MLP Classifier
  python train_onnx.py --model transformer      # Transformer (experimentell)
  python train_onnx.py --db path/to/bot.db      # Eigene DB angeben
  python train_onnx.py --min-samples 100        # Min. Samples für Training

Output:
  bingxbot_model.onnx  → Für OnnxModelInference.LoadModel() in C#
  training_report.json → Metriken und Feature-Importance
"""

import argparse
import json
import os
import sqlite3
import sys
from datetime import datetime
from pathlib import Path

import numpy as np

# Feature-Namen (müssen mit FeatureSnapshot.ToFeatureArray() übereinstimmen!)
FEATURE_NAMES = [
    "PriceVsEma20", "PriceVsEma50", "PriceVsEma200", "EmaCrossDirection",
    "RsiNormalized", "MacdHistogramNormalized", "StochKNormalized", "StochDNormalized",
    "AtrPercent", "BollingerWidth", "BollingerPosition",
    "AdxNormalized", "HtfTrend",
    "VolumeRatio",
    "FundingRate", "SessionId",
    "BtcReturn24h", "BtcTrend", "BtcCorrelation", "MarketSentiment", "FearGreedIndex",
    "OpenInterestChange",
    "FibProximity",
    "ConsecutiveUpCandles", "ConsecutiveDownCandles", "RecentReturnPercent"
]

FEATURE_COUNT = len(FEATURE_NAMES)  # 26


def get_default_db_path():
    """Standard-DB-Pfad (wie BotDatabaseService.InitializeAsync)."""
    if sys.platform == "win32":
        appdata = os.environ.get("APPDATA", "")
        return os.path.join(appdata, "BingXBot", "bot.db")
    else:
        home = os.path.expanduser("~")
        return os.path.join(home, ".local", "share", "BingXBot", "bot.db")


def load_data(db_path, min_samples=50):
    """Lädt gelabelte FeatureSnapshots aus SQLite."""
    if not os.path.exists(db_path):
        print(f"FEHLER: DB nicht gefunden: {db_path}")
        sys.exit(1)

    conn = sqlite3.connect(db_path)
    cursor = conn.cursor()

    # Spalten-Namen der Feature-Columns
    feature_cols = [f"F_{name}" for name in FEATURE_NAMES]
    cols_str = ", ".join(feature_cols)

    query = f"SELECT {cols_str}, Outcome FROM FeatureSnapshots WHERE Outcome != 0"
    cursor.execute(query)
    rows = cursor.fetchall()
    conn.close()

    if len(rows) < min_samples:
        print(f"Zu wenig Daten: {len(rows)} Samples (Min: {min_samples})")
        sys.exit(1)

    X = np.array([row[:FEATURE_COUNT] for row in rows], dtype=np.float32)
    y = np.array([1 if row[FEATURE_COUNT] > 0 else 0 for row in rows], dtype=np.int64)

    print(f"Daten geladen: {len(rows)} Samples ({sum(y)} Wins, {len(y) - sum(y)} Losses)")
    print(f"Win-Rate: {sum(y) / len(y):.1%}")
    return X, y


def train_lightgbm(X_train, y_train, X_test, y_test):
    """Trainiert LightGBM und exportiert als ONNX."""
    import lightgbm as lgb
    from skl2onnx import convert_sklearn
    from skl2onnx.common.data_types import FloatTensorType

    model = lgb.LGBMClassifier(
        n_estimators=200,
        max_depth=6,
        learning_rate=0.05,
        min_child_samples=5,
        subsample=0.8,
        colsample_bytree=0.8,
        reg_alpha=0.1,
        reg_lambda=0.1,
        random_state=42,
        verbose=-1
    )
    model.fit(X_train, y_train)

    # Feature Importance
    importance = dict(zip(FEATURE_NAMES, model.feature_importances_.tolist()))

    # ONNX Export
    initial_type = [("features", FloatTensorType([None, FEATURE_COUNT]))]
    onnx_model = convert_sklearn(model, initial_types=initial_type,
                                  target_opset=12,
                                  options={id(model): {"zipmap": False}})

    return model, onnx_model, importance


def train_mlp(X_train, y_train, X_test, y_test):
    """Trainiert MLP (Multi-Layer Perceptron) und exportiert als ONNX."""
    import torch
    import torch.nn as nn

    class MLP(nn.Module):
        def __init__(self, input_dim):
            super().__init__()
            self.net = nn.Sequential(
                nn.Linear(input_dim, 64),
                nn.ReLU(),
                nn.Dropout(0.3),
                nn.Linear(64, 32),
                nn.ReLU(),
                nn.Dropout(0.2),
                nn.Linear(32, 2)
            )

        def forward(self, x):
            logits = self.net(x)
            return torch.softmax(logits, dim=1)

    model = MLP(FEATURE_COUNT)
    optimizer = torch.optim.Adam(model.parameters(), lr=0.001, weight_decay=1e-4)
    criterion = nn.CrossEntropyLoss()

    X_t = torch.FloatTensor(X_train)
    y_t = torch.LongTensor(y_train)

    # Training
    model.train()
    for epoch in range(100):
        optimizer.zero_grad()
        output = model(X_t)
        loss = criterion(output, y_t)
        loss.backward()
        optimizer.step()

        if (epoch + 1) % 25 == 0:
            print(f"  Epoch {epoch+1}/100, Loss: {loss.item():.4f}")

    # ONNX Export
    model.eval()
    dummy = torch.randn(1, FEATURE_COUNT)
    onnx_path = "_temp_model.onnx"
    torch.onnx.export(model, dummy, onnx_path,
                       input_names=["features"],
                       output_names=["probabilities"],
                       dynamic_axes={"features": {0: "batch"}, "probabilities": {0: "batch"}},
                       opset_version=12)

    import onnx
    onnx_model = onnx.load(onnx_path)
    os.remove(onnx_path)

    return model, onnx_model, {}


def train_transformer(X_train, y_train, X_test, y_test):
    """Trainiert einen einfachen Transformer-Encoder und exportiert als ONNX."""
    import torch
    import torch.nn as nn

    class FeatureTransformer(nn.Module):
        def __init__(self, input_dim, d_model=32, nhead=4, num_layers=2):
            super().__init__()
            self.input_proj = nn.Linear(input_dim, d_model)
            encoder_layer = nn.TransformerEncoderLayer(
                d_model=d_model, nhead=nhead, dim_feedforward=64,
                dropout=0.2, batch_first=True
            )
            self.transformer = nn.TransformerEncoder(encoder_layer, num_layers=num_layers)
            self.classifier = nn.Linear(d_model, 2)

        def forward(self, x):
            # x: [batch, features] → [batch, 1, d_model]
            x = self.input_proj(x).unsqueeze(1)
            x = self.transformer(x)
            x = x.squeeze(1)  # [batch, d_model]
            logits = self.classifier(x)
            return torch.softmax(logits, dim=1)

    model = FeatureTransformer(FEATURE_COUNT)
    optimizer = torch.optim.Adam(model.parameters(), lr=0.0005, weight_decay=1e-4)
    criterion = nn.CrossEntropyLoss()

    X_t = torch.FloatTensor(X_train)
    y_t = torch.LongTensor(y_train)

    model.train()
    for epoch in range(200):
        optimizer.zero_grad()
        output = model(X_t)
        loss = criterion(output, y_t)
        loss.backward()
        optimizer.step()

        if (epoch + 1) % 50 == 0:
            print(f"  Epoch {epoch+1}/200, Loss: {loss.item():.4f}")

    model.eval()
    dummy = torch.randn(1, FEATURE_COUNT)
    onnx_path = "_temp_transformer.onnx"
    torch.onnx.export(model, dummy, onnx_path,
                       input_names=["features"],
                       output_names=["probabilities"],
                       dynamic_axes={"features": {0: "batch"}, "probabilities": {0: "batch"}},
                       opset_version=12)

    import onnx
    onnx_model = onnx.load(onnx_path)
    os.remove(onnx_path)

    return model, onnx_model, {}


def evaluate(model, X_test, y_test, model_type="lightgbm"):
    """Evaluiert das Modell auf dem Test-Set."""
    from sklearn.metrics import accuracy_score, roc_auc_score, f1_score, precision_score, recall_score

    if model_type == "lightgbm":
        y_pred = model.predict(X_test)
        y_prob = model.predict_proba(X_test)[:, 1]
    else:
        import torch
        model.eval()
        with torch.no_grad():
            output = model(torch.FloatTensor(X_test))
            y_prob = output[:, 1].numpy()
            y_pred = (y_prob > 0.5).astype(int)

    metrics = {
        "accuracy": float(accuracy_score(y_test, y_pred)),
        "auc": float(roc_auc_score(y_test, y_prob)),
        "f1": float(f1_score(y_test, y_pred)),
        "precision": float(precision_score(y_test, y_pred, zero_division=0)),
        "recall": float(recall_score(y_test, y_pred, zero_division=0)),
    }

    return metrics


def main():
    parser = argparse.ArgumentParser(description="BingXBot ONNX Model Trainer")
    parser.add_argument("--model", choices=["lightgbm", "mlp", "transformer"],
                        default="lightgbm", help="Modell-Typ (default: lightgbm)")
    parser.add_argument("--db", default=None, help="Pfad zur bot.db SQLite")
    parser.add_argument("--output", default="bingxbot_model.onnx", help="Output .onnx Datei")
    parser.add_argument("--min-samples", type=int, default=50, help="Min. Samples für Training")
    parser.add_argument("--test-split", type=float, default=0.2, help="Test-Split Ratio")
    args = parser.parse_args()

    db_path = args.db or get_default_db_path()
    print(f"DB: {db_path}")
    print(f"Modell: {args.model}")
    print(f"Features: {FEATURE_COUNT}")
    print()

    # Daten laden
    X, y = load_data(db_path, args.min_samples)

    # Train/Test Split (chronologisch, nicht random!)
    split_idx = int(len(X) * (1 - args.test_split))
    X_train, X_test = X[:split_idx], X[split_idx:]
    y_train, y_test = y[:split_idx], y[split_idx:]
    print(f"Train: {len(X_train)}, Test: {len(X_test)}")
    print()

    # Training
    print(f"Training {args.model}...")
    trainers = {
        "lightgbm": train_lightgbm,
        "mlp": train_mlp,
        "transformer": train_transformer,
    }
    model, onnx_model, importance = trainers[args.model](X_train, y_train, X_test, y_test)

    # Evaluation
    print("\nEvaluation:")
    metrics = evaluate(model, X_test, y_test, args.model)
    for k, v in metrics.items():
        print(f"  {k}: {v:.4f}")

    # AUC-Gate: Nur speichern wenn besser als Zufall
    if metrics["auc"] < 0.55:
        print(f"\nWARNUNG: AUC {metrics['auc']:.4f} < 0.55 → Modell zu schwach, wird NICHT gespeichert!")
        print("Mehr Daten sammeln oder anderen Modell-Typ probieren.")
        sys.exit(1)

    # ONNX speichern
    import onnx
    onnx.save(onnx_model, args.output)
    file_size = os.path.getsize(args.output) / 1024
    print(f"\nModell gespeichert: {args.output} ({file_size:.1f} KB)")

    # Report speichern
    report = {
        "model_type": args.model,
        "features": FEATURE_NAMES,
        "feature_count": FEATURE_COUNT,
        "train_samples": len(X_train),
        "test_samples": len(X_test),
        "metrics": metrics,
        "feature_importance": importance,
        "trained_at": datetime.utcnow().isoformat(),
        "onnx_file": args.output,
        "db_path": db_path,
    }

    report_path = args.output.replace(".onnx", "_report.json")
    with open(report_path, "w") as f:
        json.dump(report, f, indent=2)
    print(f"Report: {report_path}")

    print(f"\nVerwendung in C#:")
    print(f'  var inference = new OnnxModelInference();')
    print(f'  inference.LoadModel("{args.output}");')
    print(f'  var pWin = inference.Predict(featureSnapshot);')


if __name__ == "__main__":
    main()
