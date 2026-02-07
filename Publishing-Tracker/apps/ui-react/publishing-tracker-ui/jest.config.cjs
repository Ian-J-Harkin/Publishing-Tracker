module.exports = {
    preset: 'ts-jest',
    testEnvironment: 'jsdom',
    transform: {
        '^.+\\.tsx?$': ['ts-jest', {
            tsconfig: {
                esModuleInterop: true,
                jsx: 'react-jsx',
                skipLibCheck: true,
                target: 'es2020',
                allowJs: true,
                module: 'commonjs'
            },
            diagnostics: {
                ignoreCodes: [151001, 1343]
            }
        }],
    },
    moduleNameMapper: {
        '\\.(css|less)$': '<rootDir>/__mocks__/styleMock.js',
    },
    setupFilesAfterEnv: ['<rootDir>/src/setupTests.ts'],
    globals: {
        TextEncoder: require('util').TextEncoder,
        TextDecoder: require('util').TextDecoder,
    },
};