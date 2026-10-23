import { isAxiosError } from 'axios';

interface ProblemDetailsLike {
  detail?: string;
  title?: string;
  errors?: Record<string, string[]>;
}

/**
 * Extracts a user-friendly message from an API error, including ProblemDetails
 * bodies (409 conflicts, 429 rate limiting, validation errors, etc.).
 */
export function getApiErrorMessage(error: unknown): string {
  if (isAxiosError(error)) {
    const data = error.response?.data as ProblemDetailsLike | undefined;

    if (data?.errors) {
      const messages = Object.values(data.errors).flat();
      if (messages.length > 0) return messages.join(' ');
    }
    if (data?.detail) return data.detail;
    if (data?.title) return data.title;

    switch (error.response?.status) {
      case 409:
        return 'The request conflicts with the current state. Please refresh and try again.';
      case 429:
        return 'Too many requests. Please wait a moment and try again.';
      case 401:
        return 'Your session has expired or is invalid. Please log in again.';
      case 403:
        return 'You do not have permission to perform this action.';
      case 404:
        return 'The requested resource was not found.';
      default:
        break;
    }

    if (error.request && !error.response) {
      return 'Cannot reach the server. Please check your connection and try again.';
    }
  }

  return error instanceof Error ? error.message : 'An unexpected error occurred.';
}
