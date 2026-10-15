import js from '@eslint/js';
import vitest from '@vitest/eslint-plugin';
import { defineConfig } from 'eslint/config';
import prettier from 'eslint-config-prettier';
import { createTypeScriptImportResolver } from 'eslint-import-resolver-typescript';
import importX from 'eslint-plugin-import-x';
import jsxA11y from 'eslint-plugin-jsx-a11y';
import react from 'eslint-plugin-react';
import reactHooks from 'eslint-plugin-react-hooks';
import reactRefresh from 'eslint-plugin-react-refresh';
import simpleImportSort from 'eslint-plugin-simple-import-sort';
import unicorn from 'eslint-plugin-unicorn';
import unusedImports from 'eslint-plugin-unused-imports';
import globals from 'globals';
import tseslint from 'typescript-eslint';

const projects = ['./tsconfig.json', './tsconfig.node.json'];

// Owner package set (repository standards). Orval output is excluded: regeneration drift checks it instead.
export default defineConfig(
  {
    ignores: [
      'dist/',
      'coverage/',
      'playwright-report/',
      'test-results/',
      'src/api/generated/',
      'node_modules/',
    ],
  },
  js.configs.recommended,
  tseslint.configs.strictTypeChecked,
  {
    languageOptions: {
      parserOptions: { project: projects, tsconfigRootDir: import.meta.dirname },
    },
    settings: {
      'import-x/resolver-next': [
        createTypeScriptImportResolver({ project: projects, noWarnOnMultipleProjects: true }),
      ],
    },
  },
  importX.flatConfigs.recommended,
  importX.flatConfigs.typescript,
  unicorn.configs.recommended,
  {
    plugins: { 'simple-import-sort': simpleImportSort, 'unused-imports': unusedImports },
    rules: {
      'simple-import-sort/imports': 'error',
      'simple-import-sort/exports': 'error',
      'import-x/order': 'off',
      // TypeScript resolves default and named exports; these two report ESM plugin packages falsely.
      'import-x/no-named-as-default': 'off',
      'import-x/no-named-as-default-member': 'off',
      'no-unused-vars': 'off',
      '@typescript-eslint/no-unused-vars': 'off',
      'unused-imports/no-unused-imports': 'error',
      'unused-imports/no-unused-vars': [
        'error',
        { argsIgnorePattern: '^_', varsIgnorePattern: '^_' },
      ],
      '@typescript-eslint/restrict-template-expressions': ['error', { allowNumber: true }],
      'unicorn/prevent-abbreviations': 'off',
      'unicorn/filename-case': 'off',
      'unicorn/no-null': 'off',
    },
  },
  {
    files: ['src/**/*.{ts,tsx}', 'test/**/*.{ts,tsx}'],
    ...react.configs.flat.recommended,
    ...react.configs.flat['jsx-runtime'],
    languageOptions: {
      ...react.configs.flat.recommended.languageOptions,
      globals: globals.browser,
    },
    settings: { react: { version: 'detect' } },
  },
  // TypeScript types replace prop-types.
  { files: ['src/**/*.{ts,tsx}', 'test/**/*.{ts,tsx}'], rules: { 'react/prop-types': 'off' } },
  { files: ['src/**/*.{ts,tsx}'], ...jsxA11y.flatConfigs.recommended },
  { files: ['src/**/*.{ts,tsx}'], ...reactHooks.configs.flat.recommended },
  { files: ['src/**/*.tsx'], ...reactRefresh.configs.vite },
  {
    files: ['*.config.{ts,mjs}', 'e2e/**/*.ts'],
    languageOptions: { globals: globals.node },
  },
  // This file is JavaScript outside the TypeScript projects.
  { files: ['**/*.mjs'], ...tseslint.configs.disableTypeChecked },
  {
    files: ['src/**/*.test.{ts,tsx}', 'test/**/*.{ts,tsx}'],
    ...vitest.configs.recommended,
  },
  prettier,
);
