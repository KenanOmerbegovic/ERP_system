import { defineBoot } from '#q-app';
import { Lang } from 'quasar';
import quasarBosnian from 'quasar/lang/bs-BA';
import quasarEnglish from 'quasar/lang/en-US';
import { createI18n } from 'vue-i18n';

import messages, { defaultLanguage } from '@/i18n';

export type MessageLanguages = keyof typeof messages;
// Bosnian is the master schema; every other language must provide the same keys.
export type MessageSchema = (typeof messages)['bs-BA'];

// See https://vue-i18n.intlify.dev/guide/advanced/typescript.html#global-resource-schema-type-definition
/* eslint-disable @typescript-eslint/no-empty-object-type */
declare module 'vue-i18n' {
  // define the locale messages schema
  export interface DefineLocaleMessage extends MessageSchema {}

  // define the datetime format schema
  export interface DefineDateTimeFormat {}

  // define the number format schema
  export interface DefineNumberFormat {}
}
/* eslint-enable @typescript-eslint/no-empty-object-type */

export const languages = Object.keys(messages) as MessageLanguages[];

const quasarLanguages = { 'bs-BA': quasarBosnian, 'en-US': quasarEnglish } satisfies Record<
  MessageLanguages,
  unknown
>;

/** A per-device preference, not a secret, so localStorage is fine (tokens never go there). */
const languageStorageKey = 'erp.language';

export const i18n = createI18n<{ message: MessageSchema }, MessageLanguages, false>({
  locale: defaultLanguage,
  fallbackLocale: defaultLanguage,
  legacy: false,
  messages,
});

function isLanguage(value: string | null): value is MessageLanguages {
  return value !== null && (languages as string[]).includes(value);
}

/** Switches the app's texts and Quasar's own component texts (dates, pagination, ...). */
export function setLanguage(language: MessageLanguages): void {
  i18n.global.locale.value = language;
  // Quasar's language packs do not satisfy its own type under exactOptionalPropertyTypes.
  Lang.set(quasarLanguages[language] as Parameters<typeof Lang.set>[0]);
  document.documentElement.lang = language;
  try {
    localStorage.setItem(languageStorageKey, language);
  } catch {
    // Storage blocked (private window); the choice lasts for this session only.
  }
}

function storedLanguage(): MessageLanguages {
  try {
    const stored = localStorage.getItem(languageStorageKey);
    return isLanguage(stored) ? stored : defaultLanguage;
  } catch {
    return defaultLanguage;
  }
}

export default defineBoot(({ app }) => {
  app.use(i18n);
  setLanguage(storedLanguage());
});
