interface ErrorMessageProps {
  message: string;
  hint?: string;
}

export default function ErrorMessage({ message, hint }: ErrorMessageProps) {
  return (
    <div className="error-message" role="alert">
      <strong>{message}</strong>
      {hint && <p className="error-hint">{hint}</p>}
    </div>
  );
}
