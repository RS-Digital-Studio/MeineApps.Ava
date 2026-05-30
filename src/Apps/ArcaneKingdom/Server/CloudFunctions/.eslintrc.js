// ESLint-Konfiguration fuer die ArcaneKingdom Cloud Functions (TypeScript).
// Legacy-Format (.eslintrc), passend zu ESLint 8 + @typescript-eslint 7.
module.exports = {
  root: true,
  parser: "@typescript-eslint/parser",
  parserOptions: {
    ecmaVersion: 2022,
    sourceType: "module",
  },
  plugins: ["@typescript-eslint"],
  extends: [
    "eslint:recommended",
    "plugin:@typescript-eslint/recommended",
  ],
  env: {
    node: true,
    es2022: true,
  },
  rules: {
    // Bewusst genutzte console.log/console.warn fuer Function-Logging.
    "no-console": "off",
    // Unbenutzte Argumente mit _-Prefix erlauben.
    "@typescript-eslint/no-unused-vars": ["warn", { argsIgnorePattern: "^_" }],
  },
};
