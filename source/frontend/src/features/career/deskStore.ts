import { create } from 'zustand';

interface DeskState {
  readonly statsOpen: boolean;
  readonly toggleStats: () => void;
}

/** Transient UI state only; the career itself lives in the query cache and on the server. */
export const useDesk = create<DeskState>()((set) => ({
  statsOpen: false,
  toggleStats: () => {
    set((state) => ({ statsOpen: !state.statsOpen }));
  },
}));
