'use client';

import React from 'react';
import { AlertCircle, RefreshCw } from 'lucide-react';

export default function GlobalError({
  error,
  reset,
}: {
  error: Error & { digest?: string };
  reset: () => void;
}) {
  return (
    <html lang="en" className="dark" style={{ colorScheme: 'dark' }}>
      <body className="bg-[#090D16] text-white flex items-center justify-center min-h-screen p-6">
        <div className="bg-[#131B2E] p-8 rounded-2xl border border-[#233252] max-w-md w-full text-center space-y-4 shadow-2xl">
          <div className="w-12 h-12 rounded-full bg-rose-500/20 text-rose-400 flex items-center justify-center mx-auto">
            <AlertCircle className="w-6 h-6" />
          </div>
          <h2 className="text-lg font-bold">Admin Console Notice</h2>
          <p className="text-xs text-slate-400 leading-relaxed">
            {error?.message || 'An unexpected error occurred while loading the dashboard.'}
          </p>
          <button
            onClick={() => reset()}
            className="inline-flex items-center space-x-2 px-5 py-2.5 bg-blue-600 hover:bg-blue-500 rounded-lg text-xs font-bold text-white transition shadow-lg shadow-blue-600/30"
          >
            <RefreshCw className="w-4 h-4" />
            <span>Reload Dashboard</span>
          </button>
        </div>
      </body>
    </html>
  );
}
