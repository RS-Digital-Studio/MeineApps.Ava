// Jest-Konfiguration fuer die ArcaneKingdom Cloud Functions (TypeScript via ts-jest).
// Tests liegen unter src/**/__tests__/*.test.ts. ts-jest kompiliert sie eigenstaendig
// (unabhaengig vom Produktiv-tsc-Build, der __tests__ ausschliesst).
module.exports = {
  preset: "ts-jest",
  testEnvironment: "node",
  roots: ["<rootDir>/src"],
  testMatch: ["**/__tests__/**/*.test.ts"],
  moduleFileExtensions: ["ts", "js", "json"],
};
