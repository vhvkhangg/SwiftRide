import { initialState, readSnapshot } from './demo.ts';
import type { Role } from './demo.ts';

export const portalStorageKey = (role: Role) => `swiftride-${role}-ui-v1`;

export function readPortalSnapshot(raw: string | null, role: Role) {
  const snapshot = readSnapshot(raw);
  return snapshot.role === null || snapshot.role === role ? snapshot : initialState;
}

export function isDemoAccount(role: Role, account: string, password: string) {
  return account.trim().toLowerCase() === `${role}@swiftride.demo` && password === 'demo1234';
}
