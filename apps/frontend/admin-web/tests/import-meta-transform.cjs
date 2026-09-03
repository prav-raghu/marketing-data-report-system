const { TsJestTransformer } = require("ts-jest");

const tsJestTransformer = new TsJestTransformer({ tsconfig: "tsconfig.test.json" });

module.exports = {
    process(sourceText, sourcePath, options) {
        const patched = sourceText.replace(/import\.meta\.env/g, "process.env");
        return tsJestTransformer.process(patched, sourcePath, options);
    },
    getCacheKey(sourceText, sourcePath, options) {
        const patched = sourceText.replace(/import\.meta\.env/g, "process.env");
        return tsJestTransformer.getCacheKey(patched, sourcePath, options);
    },
};
