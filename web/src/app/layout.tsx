import type { Metadata } from 'next';
import './globals.css';

export const metadata: Metadata = {
  title: 'PHF Work Management System — Admin Console',
  description: 'Enterprise Employee Activity Logging & Task Management System',
};

export default function RootLayout({
  children,
}: Readonly<{
  children: React.ReactNode;
}>) {
  return (
    <html lang="en" className="dark" style={{ colorScheme: 'dark' }}>
      <body className="bg-[#090D16] text-slate-100 min-h-screen antialiased selection:bg-blue-600 selection:text-white">
        {children}
      </body>
    </html>
  );
}
