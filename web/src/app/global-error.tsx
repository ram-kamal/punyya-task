'use client';

export default function GlobalError({
  error,
  reset,
}: {
  error: Error & { digest?: string };
  reset: () => void;
}) {
  return (
    <html>
      <body className="bg-[#090D16] text-white flex items-center justify-center min-h-screen p-4">
        <div className="bg-[#131B2E] p-6 rounded-xl border border-[#233252] max-w-md text-center">
          <h2 className="text-lg font-bold mb-2">Something went wrong</h2>
          <p className="text-xs text-slate-400 mb-4">{error.message}</p>
          <button
            onClick={() => reset()}
            className="px-4 py-2 bg-blue-600 rounded-lg text-xs font-bold hover:bg-blue-500 transition"
          >
            Try again
          </button>
        </div>
      </body>
    </html>
  );
}
