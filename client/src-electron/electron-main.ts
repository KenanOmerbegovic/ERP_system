import { BrowserWindow, app, ipcMain, type IpcMainInvokeEvent } from 'electron';
import path from 'node:path';
import os from 'node:os';
import { registerQuasarRuntime, resolveElectronAssetsPath } from '#q-app/electron/main';

import { DesktopChannels } from './desktop-api';
import { RefreshTokenStore } from './refresh-token-store';

// needed in case process is undefined under Linux
const platform = process.platform || os.platform();

let mainWindow: BrowserWindow | undefined;

async function createWindow() {
  mainWindow = new BrowserWindow({
    icon: resolveElectronAssetsPath('icons/icon.png'), // Windows and Linux
    width: 1280,
    height: 800,
    useContentSize: true,
    webPreferences: {
      contextIsolation: true,
      nodeIntegration: false,
      sandbox: true,
      // https://v2.quasar.dev/quasar-cli-vite/developing-electron-apps/electron-preload-script
      preload: path.join(import.meta.dirname, 'electron-preload.cjs'),
    },
  });

  const appUrl = import.meta.env.QUASAR_DEV ? import.meta.env.QUASAR_APP_URL : undefined;

  // The window only ever shows the app: no pop-ups, no navigating away to other sites.
  mainWindow.webContents.setWindowOpenHandler(() => ({ action: 'deny' }));
  mainWindow.webContents.on('will-navigate', (event, url) => {
    const allowed = appUrl ? url.startsWith(appUrl) : url.startsWith('file://');
    if (!allowed) {
      event.preventDefault();
    }
  });

  if (appUrl) {
    await mainWindow.loadURL(appUrl);
  } else {
    await mainWindow.loadFile('index.html');
  }

  if (import.meta.env.QUASAR_DEBUG) {
    // if on DEV or Production with debug enabled
    mainWindow.webContents.openDevTools();
  } else {
    // we're on production; no access to devtools pls
    mainWindow.webContents.on('devtools-opened', () => {
      mainWindow?.webContents.closeDevTools();
    });
  }

  mainWindow.on('closed', () => {
    mainWindow = undefined;
  });
}

/** Only the app's own top frame may use the token store. */
function fromAppWindow(event: IpcMainInvokeEvent): boolean {
  return (
    mainWindow !== undefined &&
    event.sender === mainWindow.webContents &&
    event.senderFrame === event.sender.mainFrame
  );
}

function registerTokenStore() {
  const store = new RefreshTokenStore(path.join(app.getPath('userData'), 'session.bin'));

  ipcMain.handle(DesktopChannels.getRefreshToken, (event) =>
    fromAppWindow(event) ? store.get() : null,
  );
  ipcMain.handle(DesktopChannels.setRefreshToken, async (event, token: unknown) => {
    if (fromAppWindow(event)) {
      await store.set(token);
    }
  });
  ipcMain.handle(DesktopChannels.clearRefreshToken, async (event) => {
    if (fromAppWindow(event)) {
      await store.clear();
    }
  });
}

void app.whenReady().then(() => {
  registerQuasarRuntime();
  registerTokenStore();
  void createWindow();

  app.on('activate', () => {
    if (BrowserWindow.getAllWindows().length === 0) {
      void createWindow();
    }
  });
});

app.on('window-all-closed', () => {
  if (platform !== 'darwin') {
    app.quit();
  }
});
