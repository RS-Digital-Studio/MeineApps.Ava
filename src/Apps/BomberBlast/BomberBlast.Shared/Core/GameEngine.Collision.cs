using BomberBlast.Graphics;
using BomberBlast.Models;
using BomberBlast.Models.Entities;
using BomberBlast.Models.Grid;
using BomberBlast.Services;
using SkiaSharp;

namespace BomberBlast.Core;

/// <summary>
/// Kollisionserkennung: Spieler/Gegner/Explosionen
/// </summary>
public sealed partial class GameEngine
{
    // Positions-Index fuer Gegner — v2.0.30+ nach Core/Combat/EnemyPositionIndex.cs extrahiert.
    // Vermeidet O(n) Iteration pro Explosionszelle → O(1) Lookup, Bosse multi-cell registriert.
    private readonly BomberBlast.Core.Combat.EnemyPositionIndex _enemyPositionIndex = new();

    // Poison-Schaden-Cooldown (periodisch statt sofort-Kill)
    private float _poisonDamageTimer;
    private const float POISON_DAMAGE_COOLDOWN = 2.0f;

    // Ursprüngliche Gegner-Anzahl bei Level-Start (für Slow-Motion-Bedingung)
    private int _originalEnemyCount;

    // Statische Offsets für Splitter-Spawn (vermeidet Array-Allokation pro Kill)
    private static readonly (int dx, int dy)[] SplitterOffsets = [(-1, 0), (1, 0), (0, -1), (0, 1)];

    private void CheckCollisions()
    {
        // NOTE: Kein blanker State-Guard hier — Enemy-Kills durch Explosionen und
        // PowerUp-Collection sollen auch in Transition-States (LevelComplete, PlayerDied) laufen,
        // damit die Frames vor dem tatsaechlichen State-Wechsel konsistent zu Ende gefuehrt werden.
        // Double-Trigger-Schutz ist stattdessen in KillPlayer() (IsDying-Guard) und
        // CompleteLevel() (State-Guard) selbst implementiert.

        //.1 : Boss-Modifier Burning — Lava-Spur tötet Spieler.
        if (!_player.IsDying && !_player.HasFlamepass && !_player.IsInvincible && !_player.HasSpawnProtection)
        {
            foreach (var enemy in _enemies)
            {
                if (enemy is BossEnemy boss && boss.BurningTrail.Count > 0)
                {
                    foreach (var (tx, ty, _) in boss.BurningTrail)
                    {
                        if (_player.GridX == tx && _player.GridY == ty)
                        {
                            KillPlayer();
                            break;
                        }
                    }
                    if (_player.IsDying) break;
                }
            }
        }

        // Spieler-Kollision mit Explosionen
        foreach (var explosion in _explosions)
        {
            if (!explosion.IsActive)
                continue;

            foreach (var cell in explosion.AffectedCells)
            {
                // Block-Hit-Zellen fügen keinen Schaden zu — der Block absorbiert die Explosion.
                if (cell.IsBlockHit) continue;

                if (_player.GridX == cell.X && _player.GridY == cell.Y)
                {
                    if (!_player.HasFlamepass && !_player.IsInvincible && !_player.HasSpawnProtection)
                    {
                        KillPlayer();
                    }
                }
            }
        }

        // Spieler-Kollision mit Gegnern (nicht mehr prüfen wenn Spieler bereits stirbt)
        if (!_player.IsDying)
        foreach (var enemy in _enemies)
        {
            if (!enemy.IsActive || enemy.IsDying)
                continue;

            // Unsichtbare Ghosts können den Spieler nicht berühren
            if (enemy.IsInvisible)
                continue;

            // Getarnte Mimics sind inaktiv und harmlos
            if (enemy.IsDisguised)
                continue;

            if (_player.CollidesWith(enemy))
            {
                if (!_player.IsInvincible && !_player.HasSpawnProtection)
                {
                    // Schutzschild absorbiert 1 Gegnerkontakt (nicht Explosionen)
                    if (_player.HasShield)
                    {
                        AbsorbShield(BomberBlastColors.PowerUpCyan);
                    }
                    else
                    {
                        KillPlayer();
                    }
                }
            }
        }

        // Spieler-Schaden durch Boss-Spezial-Angriff (AttackTargetCells)
        if (!_player.IsDying && !_player.IsInvincible && !_player.HasSpawnProtection)
        {
            foreach (var enemy in _enemies)
            {
                if (enemy is not BossEnemy boss || !boss.IsActive || boss.IsDying)
                    continue;

                if (!boss.IsAttacking || boss.AttackTargetCells.Count == 0)
                    continue;

                // Spieler auf einer Angriffszelle?
                foreach (var (ax, ay) in boss.AttackTargetCells)
                {
                    if (_player.GridX == ax && _player.GridY == ay)
                    {
                        if (_player.HasShield)
                        {
                            AbsorbShield(new SKColor(255, 80, 0));
                        }
                        else
                        {
                            KillPlayer();
                        }
                        break;
                    }
                }
                if (_player.IsDying) break;
            }
        }

        // Spieler-Schaden durch Spezial-Bomben-Lava (IsLavaActive auf Zelle)
        // Elementar-Synergy: Lava verlangsamt Gegner statt Spieler zu schaden
        if (!_player.IsDying && !_player.IsInvincible && !_player.HasSpawnProtection)
        {
            var playerCell = _grid.TryGetCell(_player.GridX, _player.GridY);
            if (playerCell != null && playerCell.IsLavaActive && !_player.HasFlamepass
                && !_synergyElementalActive)
            {
                if (_player.HasShield)
                {
                    AbsorbShield(new SKColor(255, 80, 0), particleCount: 12, spread: 60f, playSound: false);
                }
                else
                {
                    KillPlayer();
                }
            }
        }

        // Spieler-Schaden durch Gift-Zellen (Poison-Bombe) - periodisch statt sofort
        if (!_player.IsDying && !_player.IsInvincible && !_player.HasSpawnProtection)
        {
            var playerPoisonCell = _grid.TryGetCell(_player.GridX, _player.GridY);
            if (playerPoisonCell != null && playerPoisonCell.IsPoisoned && !_player.HasFlamepass)
            {
                if (_poisonDamageTimer <= 0)
                {
                    if (_player.HasShield)
                    {
                        AbsorbShield(new SKColor(0, 200, 0), particleCount: 12, spread: 60f, playSound: false);
                    }
                    else
                    {
                        KillPlayer();
                    }
                    _poisonDamageTimer = POISON_DAMAGE_COOLDOWN;
                }
            }
        }

        // Spieler-Kollision mit PowerUps (Rückwärts-Iteration statt .ToList())
        for (int i = _powerUps.Count - 1; i >= 0; i--)
        {
            var powerUp = _powerUps[i];
            if (!powerUp.IsActive || powerUp.IsMarkedForRemoval || powerUp.IsBeingCollected)
                continue;

            if (_player.GridX == powerUp.GridX && _player.GridY == powerUp.GridY)
            {
                _player.CollectPowerUp(powerUp);

                // Einsammel-Animation starten (nicht sofort entfernen)
                powerUp.IsBeingCollected = true;
                powerUp.CollectTimer = Models.Entities.PowerUp.COLLECT_DURATION;

                var gridCell = _grid.TryGetCell(powerUp.GridX, powerUp.GridY);
                if (gridCell != null)
                {
                    gridCell.PowerUp = null;
                }

                // PowerUp-Collect Partikel (gold)
                _particleSystem.Emit(powerUp.X, powerUp.Y, 6, ParticleColors.PowerUpCollect, 40f, 0.4f);

                // PowerUp-Collect Floating Text
                string powerUpName = GetPowerUpShortName(powerUp.Type);
                var powerUpColor = GetPowerUpTextColor(powerUp.Type);
                _floatingText.Spawn(powerUp.X, powerUp.Y, powerUpName, powerUpColor, 13f, 1.0f);

                // Discovery-Hint bei Erstentdeckung (nur für nicht-triviale PowerUps)
                if (powerUp.Type.GetUnlockLevel() > 1)
                {
                    TryShowDiscoveryHint("powerup_" + powerUp.Type.ToString().ToLower());
                }

                // Tutorial: PowerUp-Schritt abgeschlossen
                _tutorialService.CheckStepCompletion(TutorialStepType.CollectPowerUp);

                _soundManager.PlaySound(SoundManager.SFX_POWERUP);
                _vibration.VibratePickUp();
                ScoreChanged?.Invoke(_player.Score);

                // Tracking: PowerUp eingesammelt (Missionen + Sammlung)
                _tracking.OnPowerUpCollected(powerUp.Type.ToFastString());
            }
        }

        // Spieler-Kollision mit Exit (gecachte Position statt Grid-Iteration)
        if (_exitRevealed && _exitCell != null)
        {
            if (_player.GridX == _exitCell.X && _player.GridY == _exitCell.Y)
            {
                // Sicherstellen dass ALLE Gegner besiegt sind (inkl. nachträglich gespawnter Pontans)
                bool allEnemiesDead = true;
                foreach (var enemy in _enemies)
                {
                    if (enemy.IsActive && !enemy.IsDying)
                    {
                        allEnemiesDead = false;
                        break;
                    }
                }

                if (allEnemiesDead)
                {
                    // Tutorial: Exit-Schritt abgeschlossen
                    _tutorialService.CheckStepCompletion(TutorialStepType.FindExit);
                    CompleteLevel();
                }
                else
                {
                    // Feedback: Spieler steht auf Exit, aber Gegner leben noch (mit Cooldown)
                    if (_defeatAllCooldown <= 0)
                    {
                        _floatingText.Spawn(_player.X, _player.Y - 16,
                            _localizationService.GetString("FloatDefeatAll") ?? "DEFEAT ALL!",
                            SKColors.Red, 14f, 1.5f);
                        _defeatAllCooldown = 2f; // Nur alle 2 Sekunden anzeigen
                    }
                }
            }
        }

        // Gegner-Kollision mit Explosionen (Position-Index → O(1) Lookup statt O(n) pro Zelle).
        // Index wird LAZY gebaut: Nur wenn wir tatsaechlich auf eine aktive Explosion stossen.
        // In ~95% der Frames ist keine Explosion aktiv → Rebuild gespart (~16-20 Dict-Ops/Frame).
        bool indexBuilt = false;
        foreach (var explosion in _explosions)
        {
            if (!explosion.IsActive)
                continue;

            if (!indexBuilt)
            {
                _enemyPositionIndex.Rebuild(_enemies);
                indexBuilt = true;
            }

            foreach (var cell in explosion.AffectedCells)
            {
                // Block-Hit-Zellen fügen keinen Schaden zu — der Block absorbiert die Explosion
                // auch wenn er während der Explosion-Dauer zerfällt (Block-Destroy = 0.3s,
                // Explosion-Duration = 0.9s → Zelle wird vorzeitig frei, aber Gegner die
                // dort einlaufen sollen NICHT sterben — sonst wirkt es als hätte die
                // Explosion "durch den Block durchgeschossen").
                if (cell.IsBlockHit) continue;

                if (!_enemyPositionIndex.TryGetAt(cell.X, cell.Y, out var enemiesAtCell))
                    continue;

                for (int i = enemiesAtCell.Count - 1; i >= 0; i--)
                {
                    var enemy = enemiesAtCell[i];
                    if (!enemy.IsActive || enemy.IsDying)
                        continue;

                    // Ghost: Unsichtbare Ghosts sind immun gegen Explosionen
                    if (enemy.IsInvisible)
                        continue;

                    //.1 : Shielded-Modifier — Schild absorbiert 1 Hit pro
                    // Recharge-Cooldown. Kein Schaden, sichtbares Feedback, Schild-Recharge laeuft an.
                    if (enemy is BossEnemy bossShield && bossShield.ConsumeShieldHit())
                    {
                        _floatingText.Spawn(enemy.X, enemy.Y - 16,
                            _localizationService.GetString("FloatShieldBlock") ?? "SHIELDED!",
                            new SKColor(120, 200, 255), 16f, 1.2f);
                        _particleSystem.Emit(enemy.X, enemy.Y, 10, new SKColor(120, 200, 255), 80f, 0.4f);
                        _vibration.VibrateShieldHit();
                        continue;
                    }

                    // Tanker/Boss: Multi-Hit - TakeDamage gibt true zurück wenn tot
                    if (enemy.HitPoints > 1)
                    {
                        if (enemy.TakeDamage())
                        {
                            KillEnemy(enemy);
                        }
                        else
                        {
                            // Überlebt - visuelles Feedback
                            var (hr, hg, hb) = enemy.Type.GetColor();
                            _particleSystem.Emit(enemy.X, enemy.Y, 6, new SKColor(hr, hg, hb), 60f, 0.3f);

                            //.1 : Reflective-Modifier — 30% Chance dass der Boss
                            // den Hit auf den Spieler zurueckspiegelt. Deterministisch via EngineRngNext.
                            if (enemy is BossEnemy bossReflect
                                && bossReflect.Modifier == Models.Entities.BossModifier.Reflective
                                && !_player.IsInvincible && !_player.HasSpawnProtection && !_player.HasFlamepass
                                && EngineRngNext(100) < 30)
                            {
                                _floatingText.Spawn(_player.X, _player.Y - 16,
                                    _localizationService.GetString("FloatReflected") ?? "REFLECTED!",
                                    new SKColor(255, 80, 80), 14f, 1.0f);
                                KillPlayer();
                            }

                            // Boss: HP-Anzeige + stärkerer Shake
                            if (enemy is BossEnemy bossHit)
                            {
                                var hitBossText = string.Format(
                                    _localizationService.GetString("FloatHitBoss") ?? "HIT! {0}/{1}",
                                    bossHit.HitPoints, bossHit.MaxHitPoints);
                                _floatingText.Spawn(enemy.X, enemy.Y - 16,
                                    hitBossText,
                                    new SKColor(255, 100, 50), 16f, 1.2f);
                                _screenShake.Trigger(3f, 0.2f);
                                    _vibration.VibrateMedium();

                                // Enrage-Warnung bei 50% HP (VOR BossEnemy.Update, daher !IsEnraged + HP-Check)
                                if (!bossHit.IsEnraged && bossHit.HitPoints <= bossHit.MaxHitPoints / 2)
                                {
                                    _floatingText.Spawn(enemy.X, enemy.Y - 32,
                                        _localizationService.GetString("FloatEnraged") ?? "ENRAGED!",
                                        new SKColor(255, 0, 0), 18f, 1.5f);
                                    _particleSystem.Emit(enemy.X, enemy.Y, 12,
                                        new SKColor(255, 50, 0), 100f, 0.6f);
                                }
                            }
                            else
                            {
                                _floatingText.Spawn(enemy.X, enemy.Y - 12,
                                    _localizationService.GetString("FloatHit") ?? "HIT!",
                                    new SKColor(255, 200, 50), 14f, 1.0f);
                                _screenShake.Trigger(2f, 0.15f);
                            }
                        }
                    }
                    else
                    {
                        KillEnemy(enemy);
                    }
                }
            }
        }
    }

    private void KillPlayer()
    {
        if (_player.IsDying)
            return;

        _playerDamagedThisLevel = true;
        _fortressRegenTimer = 0; // Festungs-Synergy: Timer bei Schaden zurücksetzen
        _deathsInLevel++;        //.2 : Tode pro Level fuer Funnel-Telemetrie

        //.3 : Roter Damage-Flash am Bildschirmrand fuer Hit-Feedback.
        // Snappy 50ms Attack + 250ms Decay = 300ms total — kuerzer als ULTRA-Combo-Flash
        // damit Spieler nicht von Vignette ueberlagert wird waehrend er reagiert.
        // Bei ReducedEffects-Toggle uebersprungen (Photosensitivity-Schutz).
        if (!_inputManager.ReducedEffects)
            _damageFlash.TriggerWithDuration(new SKColor(220, 40, 40), 0.05f, 0.25f);

        _player.Kill();
        _timer.Pause();
        _state = GameState.PlayerDied;
        _stateTimer = 0;

        //.x : OnPlayerHit-Hook — Modi koennen z.B. Run-Abbruch-Logik anhaengen.
        try { _currentMode?.OnPlayerHit(BuildModeContext()); }
        catch { /* Best-Effort, no-op-Default in GameModeBase */ }

        // Game-Feel: Stärkerer Shake + Hit-Pause + Vibration bei Spieler-Tod
        _screenShake.Trigger(5f, 0.3f);
        _hitPauseTimer = 0.1f;
        _vibration.VibrateDeath();

        // v2.0.46 — Audio-Caption für gehörlose Spieler
        if (_accessibility?.SubtitlesEnabled == true)
        {
            _subtitles.Show(_localizationService.GetString("SubtitlePlayerDeath") ?? "[YOU DIED]");
        }

        // Tod-Partikel: Burst nach außen (orange/rot)
        _particleSystem.EmitShaped(_player.X, _player.Y, 16, new SKColor(255, 100, 30),
            ParticleShape.Circle, 120f, 0.6f, 3f, hasGlow: true);
        _particleSystem.EmitShaped(_player.X, _player.Y, 8, new SKColor(255, 50, 0),
            ParticleShape.Circle, 80f, 0.4f, 2.5f);
        _particleSystem.EmitExplosionSparks(_player.X, _player.Y, 10, new SKColor(255, 180, 50), 140f);

        _soundManager.PlaySound(SoundManager.SFX_PLAYER_DEATH);
    }

    private void KillEnemy(Enemy enemy, Bomb? sourceBomb = null)
    {
        enemy.Kill();
        _enemiesRemainingDirty = true;
        _enemiesKilled++;

        //.x : OnEnemyKilled-Hook — Modi koennen Spawn-Eskalation o.ae. anhaengen.
        try { _currentMode?.OnEnemyKilled(BuildModeContext()); }
        catch { /* Best-Effort, no-op-Default in GameModeBase */ }

        // Boss: Eigene Punkte statt EnemyType-Points
        int points = enemy is BossEnemy bossKilled ? bossKilled.BossPoints : enemy.Points;

        //.x : Mode-Score-Modifier (Default 1.0 = kein Effekt).
        // Auf `points` selbst angewandt → Score UND FloatingText-Anzeige bleiben konsistent.
        float scoreModifier = _currentMode?.GetScoreModifier(BuildModeContext()) ?? 1.0f;
        if (Math.Abs(scoreModifier - 1.0f) > 0.001f)
            points = (int)Math.Round(points * scoreModifier);

        // Phase 30d — Co-Op: Score an den Spieler der die Bombe gelegt hat.
        // Default (Single-Player oder Source-unbekannt): Player 1.
        var scoringPlayer = ResolveScoringPlayer(sourceBomb);
        scoringPlayer.Score += points;

        // Boss-Tod: Extra Celebration
        if (enemy is BossEnemy deadBoss)
        {
            _floatingText.Spawn(enemy.X, enemy.Y,
                string.Format(_localizationService.GetString("FloatBossDefeated") ?? "BOSS DEFEATED! +{0}", points),
                BomberBlastColors.Gold, 20f, 2.0f);
            _screenShake.Trigger(6f, 0.4f);
            // Phase 21 (V4) — Boss-Kill ist der ultimative Big-Hit: voller Camera-Pull-Back + Victory-Stinger
            _screenShake.TriggerPullBack(magnitude: 1.0f, durationSeconds: 0.6f);
            _soundManager.PlayStinger(SoundManager.STINGER_VICTORY);
            _particleSystem.EmitShaped(enemy.X, enemy.Y, 24, BomberBlastColors.Gold,
                ParticleShape.Circle, 140f, 1.0f, 3.5f, hasGlow: true);
            _particleSystem.EmitExplosionSparks(enemy.X, enemy.Y, 16, new SKColor(255, 200, 50), 180f);
            _soundManager.PlaySound(SoundManager.SFX_LEVEL_COMPLETE);

            // v2.0.47 — Audio-Caption: Boss-Defeat-Fanfare für gehörlose Spieler
            if (_accessibility?.SubtitlesEnabled == true)
            {
                var subKey = _localizationService.GetString("SubtitleBossDefeat") ?? "[BOSS DEFEATED]";
                _subtitles.Show(subKey);
            }

            // Tracking: Boss-Kill (Achievement + Liga + BattlePass + Collection + Missionen)
            var bossFlag = deadBoss.BossKind switch
            {
                BossType.StoneGolem => 1,
                BossType.IceDragon => 2,
                BossType.FireDemon => 4,
                BossType.ShadowMaster => 8,
                BossType.FinalBoss => 16,
                _ => 0
            };
            float bossTime = _currentLevel?.TimeLimit - _timer.RemainingTime ?? 0f;
            _tracking.OnBossKilled(deadBoss.BossKind, bossFlag, bossTime);
        }
        else
        {
            // Score-Popup über dem Gegner (normal)
            _floatingText.Spawn(enemy.X, enemy.Y, $"+{points}", BomberBlastColors.Gold, 14f);
        }

        // v2.0.54 — Phase 12: Combo-Logic in ComboSystem extrahiert (Pure-Logic, isoliert testbar).
        _comboSystem.RegisterKill();

        if (_comboSystem.Count >= 2)
        {
            int comboBonus = _comboSystem.GetScoreBonus();

            // Kettenreaktions-Bonus: Bei 3+ Combo wahrscheinlich eine Kette → 50% extra
            bool isChainKill = _comboSystem.IsChainKill;
            if (isChainKill)
            {
                comboBonus = (int)(comboBonus * 1.5f);
            }

            // Phase 30d — Combo-Bonus an den scoring-Spieler (gleicher Pfad wie Kill-Score)
            scoringPlayer.Score += comboBonus;

            // Kettenreaktions-Text hat Vorrang vor normalem Combo-Text
            string comboText;
            SKColor comboColor;
            if (isChainKill)
            {
                comboText = string.Format(
                    _localizationService.GetString("FloatChain") ?? "CHAIN x{0}!", _comboCount);
                comboColor = new SKColor(255, 200, 0); // Gold für Chain-Kills
            }
            else
            {
                comboText = $"x{_comboCount}!";
                comboColor = new SKColor(255, 150, 0); // Orange für niedrige Combos
            }
            // Hierarchie: ULTRA (x10+) > MEGA (x5+) > CHAIN (x3+) > x2
            if (_comboCount >= 10)
                comboText = string.Format(
                    _localizationService.GetString("FloatUltra") ?? "ULTRA x{0}!", _comboCount);
            else if (_comboCount >= 5)
                comboText = string.Format(
                    _localizationService.GetString("FloatMega") ?? "MEGA x{0}!", _comboCount);
            // Farbverlauf: x4-x6 Rot, x7-x9 Magenta, x10+ Gold-Glow
            if (_comboCount >= 10) comboColor = BomberBlastColors.Gold;          // Gold fuer ULTRA
            else if (_comboCount >= 7) comboColor = new SKColor(255, 0, 200);      // Magenta hoch
            else if (_comboCount >= 4) comboColor = new SKColor(255, 50, 0);       // Rot fuer hohe Combos

            // Phase 22b — Crit-Indicator (G3 aus Audit): Größen-Pop + längere Lifetime bei höherer Combo-Stufe.
            // Hades-Pattern: ULTRA-Hits sind sofort als visuell "epischer" lesbar.
            //   x2-x3: 18f, 1.5s (Standard)
            //   x4-x6: 22f, 1.8s (CHAIN/MEGA-Edge)
            //   x7-x9: 26f, 2.0s
            //   x10+: 32f, 2.5s (ULTRA — Größen-Pop ×1.78, dauert deutlich länger)
            float comboFontSize = _comboCount switch
            {
                >= 10 => 32f,
                >= 7 => 26f,
                >= 4 => 22f,
                _ => 18f,
            };
            float comboDuration = _comboCount switch
            {
                >= 10 => 2.5f,
                >= 7 => 2.0f,
                >= 4 => 1.8f,
                _ => 1.5f,
            };
            _floatingText.Spawn(enemy.X, enemy.Y - 12, comboText, comboColor, comboFontSize, comboDuration);

            // v2.0.48 — Audio-Caption für Ultra-Combo (x10+) — gehörlose Spieler bekommen Feedback
            // Throttling: Nur alle 5 Combos einen Subtitle damit nicht spammt
            if (_comboCount >= 10 && _comboCount % 5 == 0 && _accessibility?.SubtitlesEnabled == true)
            {
                _subtitles.Show(_localizationService.GetString("SubtitleUltraCombo") ?? "[ULTRA COMBO]");
            }

            // Phase 21 (V4) — Camera-Pull-Back bei Big-Combos. Stinger-Trigger über SoundManager:
            //  - MEGA (x5+): leichter Pull-Back, MEGA-Stinger
            //  - ULTRA (x10+): starker Pull-Back, ULTRA-Stinger
            if (_comboCount == 10)
            {
                _screenShake.TriggerPullBack(magnitude: 1.0f, durationSeconds: 0.5f);
                _soundManager.PlayStinger(SoundManager.STINGER_COMBO_ULTRA);
                //.2 : Vollbild-Vignette-Flash beim Erreichen von ULTRA.
                // Welt-Akzent-Farbe als Vignette-Tint (Schattenwelt violett, Vulkan orange, ...).
                // Bei ReducedEffects-Toggle stillschweigend uebersprungen (Flash kann
                // photosensitive Spieler triggern).
                if (!_inputManager.ReducedEffects)
                    _ultraFlash.Trigger(_renderer.GetWorldAccentColor());
                //.3 : Music-Boost bei ULTRA — der epische Moment.
                _soundManager.BusMixer.Boost(BomberBlast.Core.Audio.AudioBus.Music, 1.25f, 5.0f);
                // Combo-Tier-Telemetrie (ULTRA) ehemals via IAnalyticsService — Analytics ist deaktiviert.
                _comboTiersInLevel++;
            }
            else if (_comboCount == 5)
            {
                _screenShake.TriggerPullBack(magnitude: 0.5f, durationSeconds: 0.35f);
                _soundManager.PlayStinger(SoundManager.STINGER_COMBO_MEGA);
                // Combo-Tier-Telemetrie (MEGA) ehemals via IAnalyticsService — Analytics ist deaktiviert.
                _comboTiersInLevel++;
            }

            // Tracking: Combo erreicht (Achievement + Missionen)
            _tracking.OnComboReached(_comboCount);
        }

        // Midas-Synergy: Gegner droppen Mini-Coins bei Tod
        if (_synergyMidasActive && _isDungeonRun)
        {
            int midasCoins = enemy is BossEnemy ? 50 : 10;
            // Phase 30d — Midas-Coins an den scoring-Spieler
            scoringPlayer.Score += midasCoins;
            _floatingText.Spawn(enemy.X + 8, enemy.Y + 8, $"+{midasCoins}",
                BomberBlastColors.Gold, 11f, 0.8f);
            _particleSystem.Emit(enemy.X, enemy.Y, 4, BomberBlastColors.Gold, 40f, 0.4f);
        }

        // Game-Feel: Hit-Pause + Partikel bei Enemy-Kill
        _hitPauseTimer = 0.05f;
        var (r, g, b) = enemy.Type.GetColor();
        _particleSystem.Emit(enemy.X, enemy.Y, 10, new SKColor(r, g, b), 80f, 0.5f);
        _particleSystem.Emit(enemy.X, enemy.Y, 4, ParticleColors.EnemyDeathLight, 50f, 0.3f);

        _soundManager.PlaySound(SoundManager.SFX_ENEMY_DEATH);
        ScoreChanged?.Invoke(_player.Score);

        // Tracking: Enemy-Kill (Achievement + Collection + Missionen)
        _tracking.OnEnemyKilled(enemy.Type, _isSurvivalMode);

        // Slow-Motion bei letztem Kill (nur bei ≥4 Gegnern, Boss-Level oder Survival) oder hohem Combo (x4+)
        bool isLastEnemy = true;
        foreach (var e in _enemies)
        {
            if (e != enemy && e.IsActive && !e.IsDying)
            {
                isLastEnemy = false;
                break;
            }
        }
        bool isBossLevel = _currentLevel?.BossKind != null;
        bool slowMotionWorthy = isLastEnemy && (_originalEnemyCount >= 4 || isBossLevel || _isSurvivalMode);
        if ((slowMotionWorthy || _comboSystem.Count >= 4) && !_inputManager.ReducedEffects)
        {
            // v2.0.37: ULTRA-Combo (x10+) verlaengert die Slow-Mo um 50% — visueller Belohnungs-Peak
            _slowMotionTimer = _comboSystem.GetSlowMotionDuration(SLOW_MOTION_DURATION);
        }

        //.3 : Adaptive-Music-Boost bei Last-Enemy-Drama (+20% Music fuer 4s).
        // Last-Enemy-Moment ist filmisch wertvoll — Music-Boost macht den Kill-Shot epischer.
        if (isLastEnemy && (_originalEnemyCount >= 3 || isBossLevel))
        {
            _soundManager.BusMixer.Boost(BomberBlast.Core.Audio.AudioBus.Music, 1.20f, 4.0f);
        }

        // Splitter-Logik: Beim Tod 2 Mini-Splitter spawnen
        if (enemy.Type.SplitsOnDeath() && !enemy.IsMiniSplitter)
        {
            int spawned = 0;
            foreach (var (ox, oy) in SplitterOffsets)
            {
                if (spawned >= 2) break;
                int nx = enemy.GridX + ox;
                int ny = enemy.GridY + oy;
                var splitCell = _grid.TryGetCell(nx, ny);
                if (splitCell != null && splitCell.Type != CellType.Wall && splitCell.Type != CellType.Block && splitCell.Bomb == null)
                {
                    var mini = Enemy.CreateMiniSplitterAtGrid(nx, ny);
                    _enemies.Add(mini);
                    _enemiesRemainingDirty = true;
                    spawned++;

                    // Spawn-Partikel
                    var (sr, sg, sb) = EnemyType.Splitter.GetColor();
                    _particleSystem.Emit(mini.X, mini.Y, 6, new SKColor(sr, sg, sb), 60f, 0.3f);
                }
            }
        }

        // Prüfen ob alle Gegner besiegt
        CheckExitReveal();

        // Tutorial: DefeatEnemies-Schritt abgeschlossen wenn letzter Gegner besiegt
        if (isLastEnemy)
        {
            _tutorialService.CheckStepCompletion(TutorialStepType.DefeatEnemies);
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // POWERUP FLOATING TEXT HELPER
    // ═══════════════════════════════════════════════════════════════════════

    private string GetPowerUpShortName(PowerUpType type) => type switch
    {
        PowerUpType.BombUp => _localizationService.GetString("FloatBombUp") ?? "+BOMB",
        PowerUpType.Fire => _localizationService.GetString("FloatFireUp") ?? "+FIRE",
        PowerUpType.Speed => _localizationService.GetString("FloatSpeedUp") ?? "+SPEED",
        PowerUpType.Wallpass => _localizationService.GetString("FloatWallpass") ?? "+WALL",
        PowerUpType.Detonator => _localizationService.GetString("FloatDetonator") ?? "+DET",
        PowerUpType.Bombpass => _localizationService.GetString("FloatBombpass") ?? "+BPASS",
        PowerUpType.Flamepass => _localizationService.GetString("FloatFlamepass") ?? "+FLAME",
        PowerUpType.Mystery => _localizationService.GetString("FloatInvincible") ?? "+INVINCIBLE",
        PowerUpType.Kick => _localizationService.GetString("FloatKick") ?? "+KICK",
        PowerUpType.LineBomb => _localizationService.GetString("FloatLineBomb") ?? "+LINE",
        PowerUpType.PowerBomb => _localizationService.GetString("FloatPowerBomb") ?? "+POWER",
        PowerUpType.Skull => _localizationService.GetString("FloatCursed") ?? "CURSED!",
        PowerUpType.Cure => _localizationService.GetString("FloatCure") ?? "HEALED!",
        _ => "+???"
    };

    private static SKColor GetPowerUpTextColor(PowerUpType type) => type switch
    {
        PowerUpType.BombUp => new SKColor(100, 150, 255),
        PowerUpType.Fire => new SKColor(255, 130, 50),
        PowerUpType.Speed => new SKColor(80, 255, 100),
        PowerUpType.Wallpass => new SKColor(200, 150, 80),
        PowerUpType.Detonator => new SKColor(255, 80, 80),
        PowerUpType.Bombpass => new SKColor(120, 120, 255),
        PowerUpType.Flamepass => new SKColor(255, 220, 60),
        PowerUpType.Mystery => new SKColor(200, 120, 255),
        PowerUpType.Kick => new SKColor(255, 165, 0),
        PowerUpType.LineBomb => new SKColor(0, 200, 255),
        PowerUpType.PowerBomb => new SKColor(255, 50, 50),
        PowerUpType.Skull => new SKColor(100, 0, 100),
        PowerUpType.Cure => new SKColor(40, 200, 80),
        _ => SKColors.White
    };
}
