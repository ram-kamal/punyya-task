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
    <html lang="en">
      <body className="bg-darkBg text-slate-100 min-h-screen antialiased">
        {children}
      </body>
    </html>
  );
}
