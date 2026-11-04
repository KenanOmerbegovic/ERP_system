import { safeStorage } from 'electron';
import { readFile, rm, writeFile } from 'node:fs/promises';

/** Refresh tokens are opaque base32/base64 strings well under this size; anything else is rejected. */
const MaxTokenLength = 1024;

/**
 * Keeps the desktop refresh token encrypted with the OS keychain (safeStorage: DPAPI on Windows,
 * Keychain on macOS, the secret service on Linux). Where no keychain is available the token is
 * kept in memory only, so the user signs in again after restarting the app.
 */
export class RefreshTokenStore {
  readonly #file: string;
  #inMemory: string | null = null;

  constructor(file: string) {
    this.#file = file;
  }

  async get(): Promise<string | null> {
    if (!safeStorage.isEncryptionAvailable()) {
      return this.#inMemory;
    }

    try {
      return safeStorage.decryptString(await readFile(this.#file));
    } catch {
      // No session saved yet, or one encrypted for another OS user: treat as signed out.
      return null;
    }
  }

  async set(token: unknown): Promise<void> {
    if (typeof token !== 'string' || token.length === 0 || token.length > MaxTokenLength) {
      throw new TypeError('Invalid refresh token.');
    }

    if (!safeStorage.isEncryptionAvailable()) {
      this.#inMemory = token;
      return;
    }

    await writeFile(this.#file, safeStorage.encryptString(token), { mode: 0o600 });
  }

  async clear(): Promise<void> {
    this.#inMemory = null;
    await rm(this.#file, { force: true });
  }
}
