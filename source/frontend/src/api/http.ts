import axios, { type AxiosRequestConfig, mergeConfig } from 'axios';

/** One HTTP client for the API: relative URLs (Vite proxies /v1 locally; one origin in production). */
export const http = axios.create({ timeout: 30_000 });

/** An API failure: `status` is null when the request never got a response (offline, timeout). */
export class ApiRequestError extends Error {
  constructor(
    readonly status: number | null,
    readonly code: string,
    readonly detail: string | null,
    readonly errors: Record<string, string[]>,
  ) {
    super(`${code}${status === null ? '' : ` (${status})`}`);
    this.name = 'ApiRequestError';
  }

  get isNetwork(): boolean {
    return this.status === null;
  }
}

/** Orval types every hook error with this alias; the mutator only ever rejects with ApiRequestError. */
export type ErrorType<_E> = ApiRequestError;

export function isApiError(error: unknown): error is ApiRequestError {
  return error instanceof ApiRequestError;
}

interface ProblemBody {
  code?: unknown;
  detail?: unknown;
  errors?: unknown;
}

function toApiError(error: unknown): ApiRequestError {
  if (!axios.isAxiosError(error) || !error.response) {
    return new ApiRequestError(null, 'network-error', null, {});
  }

  const status = error.response.status;
  const body = (
    typeof error.response.data === 'object' && error.response.data !== null
      ? error.response.data
      : {}
  ) as ProblemBody;
  const code = typeof body.code === 'string' ? body.code : `http-${status}`;
  const detail = typeof body.detail === 'string' ? body.detail : null;
  const errors =
    typeof body.errors === 'object' && body.errors !== null
      ? (body.errors as Record<string, string[]>)
      : {};
  return new ApiRequestError(status, code, detail, errors);
}

/** Orval mutator: per-call options (a career's Authorization header) merge into the generated request. */
export async function apiRequest<T>(
  config: AxiosRequestConfig,
  options?: AxiosRequestConfig,
): Promise<T> {
  try {
    // mergeConfig keeps url, method and data from its second argument and deep-merges headers.
    const response = await http.request<T>(mergeConfig(options ?? {}, config));
    return response.data;
  } catch (error) {
    throw toApiError(error);
  }
}
