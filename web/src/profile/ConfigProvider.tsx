import { createContext, useContext, type ReactNode } from 'react';
import { api } from '../api/client';
import type { AppConfig } from '../api/types';
import { todayIn } from '../lib/dates';
import { useApi } from '../lib/useApi';

const fallback: AppConfig = { version: '', timeZone: 'Europe/London', today: '', onlineEnabled: true };

const ConfigContext = createContext<AppConfig>(fallback);

export function ConfigProvider({ children }: { children: ReactNode }) {
  const { data } = useApi((s) => api.get<AppConfig>('/api/config', s), []);
  return <ConfigContext.Provider value={data ?? fallback}>{children}</ConfigContext.Provider>;
}

export function useConfig(): AppConfig {
  return useContext(ConfigContext);
}

/** Today's date in the server's timezone (so all devices agree on the diary day). */
export function useToday(): string {
  const { timeZone } = useConfig();
  return todayIn(timeZone);
}
