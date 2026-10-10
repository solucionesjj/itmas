/** Index of the user manual (`public/manual/index.json`, SPEC-0004 §8.1). */
export interface ManualIndex {
  readonly categories: readonly ManualCategory[];
  readonly processes: readonly ManualProcess[];
}

export interface ManualCategory {
  readonly id: string;
  readonly title: string;
}

export interface ManualProcess {
  /** Also the name of its Markdown file (`<id>.md`) and of its page (`/manual/<id>`). */
  readonly id: string;
  readonly title: string;
  readonly summary: string;
  readonly category: string;
  /** Permissions it needs (or a short description such as "Root"); empty when anyone can do it. */
  readonly permissions: readonly string[];
  readonly specs: readonly string[];
  /** Date of the last update, ISO `YYYY-MM-DD`. */
  readonly updated: string;
  /** Application screens it explains; every navigation entry must be covered (RF-7). */
  readonly routes: readonly string[];
}
