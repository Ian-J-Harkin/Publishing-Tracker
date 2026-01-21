module.exports = {
    preset: 'ts-jest',
    testEnvironment: 'jsdom',
    transform: {
        '^.+\\.tsx?$': 'ts-jest',
    },
    moduleNameMapper: {
        '\\.(css|less)$': '<rootDir>/__mocks__/styleMock.js',
    },
    setupFilesAfterEnv: ['<rootDir>/src/setupTests.ts'],
    globals: {
        'ts-jest': {
            diagnostics: {
                ignoreCodes: [151001]
            }
        },
        TextEncoder: require('util').TextEncoder,
        TextDecoder: require('util').TextDecoder,
    },
};