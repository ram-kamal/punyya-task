'use client';

import React from 'react';
import Link from 'next/link';
import { 
  Download, 
  Monitor, 
  ShieldCheck, 
  CheckCircle2, 
  ArrowRight, 
  Laptop, 
  Clock, 
  Key, 
  Heart,
  FileArchive,
  Info
} from 'lucide-react';

export default function DownloadPage() {
  const exeUrl = '/task/downloads/PunnyaHeartDesk.exe';
  const zipUrl = '/task/downloads/PunnyaHeartDesk-Windows.zip';

  return (
    <div className="min-h-screen bg-[#0D0E12] text-slate-100 flex flex-col font-sans antialiased selection:bg-rose-500 selection:text-white">
      {/* Background ambient glows */}
      <div className="fixed top-0 left-1/2 -translate-x-1/2 w-full max-w-7xl h-96 bg-gradient-to-b from-rose-600/10 via-rose-500/5 to-transparent blur-3xl pointer-events-none -z-10" />

      {/* Header */}
      <header className="border-b border-[#262938] bg-[#151720]/90 backdrop-blur-md sticky top-0 z-30 px-4 sm:px-8 py-3.5">
        <div className="max-w-6xl mx-auto flex items-center justify-between">
          <Link href="/" className="flex items-center space-x-3 group">
            <div className="w-10 h-10 rounded-xl bg-gradient-to-br from-rose-600 via-rose-500 to-pink-600 flex items-center justify-center font-black text-white shadow-lg shadow-rose-600/25 group-hover:scale-105 transition-transform">
              <Heart className="w-5 h-5 fill-white" />
            </div>
            <div>
              <div className="text-base font-bold text-white tracking-tight flex items-center space-x-2">
                <span>Punnya Heart Foundation</span>
                <span className="text-[10px] bg-rose-500/20 text-rose-300 font-semibold px-2 py-0.5 rounded-full border border-rose-500/30">
                  Staff Desk
                </span>
              </div>
              <p className="text-xs text-slate-400">Workstation Telemetry & Task Management System</p>
            </div>
          </Link>

          <Link
            href="/"
            className="inline-flex items-center space-x-2 px-3.5 py-1.5 rounded-xl bg-[#1C1F2B] hover:bg-[#262A3B] border border-[#2E3347] text-xs font-semibold text-slate-200 transition"
          >
            <span>Admin Portal</span>
            <ArrowRight className="w-3.5 h-3.5 text-rose-400" />
          </Link>
        </div>
      </header>

      {/* Hero Section */}
      <main className="flex-1 max-w-5xl mx-auto px-4 sm:px-8 py-10 sm:py-16 w-full">
        <div className="text-center max-w-3xl mx-auto mb-12">
          <div className="inline-flex items-center space-x-2 px-3 py-1 rounded-full bg-rose-500/10 border border-rose-500/20 text-rose-400 text-xs font-semibold mb-4">
            <Monitor className="w-3.5 h-3.5" />
            <span>Official Desktop Client • Windows 64-bit</span>
          </div>
          <h1 className="text-3xl sm:text-5xl font-black text-white tracking-tight leading-tight mb-4">
            Download <span className="bg-gradient-to-r from-rose-400 via-rose-300 to-pink-400 bg-clip-text text-transparent">Punnya Heart Desk</span>
          </h1>
          <p className="text-slate-300 text-sm sm:text-base leading-relaxed">
            The dedicated desktop application for staff members. Enables automatic shift clock-in, 
            live task tracking, deliverable submissions, and tamper-proof offline activity sync.
          </p>
        </div>

        {/* Primary Download Cards */}
        <div className="grid grid-cols-1 md:grid-cols-2 gap-6 mb-14">
          {/* Card 1: Standalone Portable Exe (Recommended) */}
          <div className="bg-[#151720] border-2 border-rose-500/40 hover:border-rose-500 rounded-2xl p-6 sm:p-8 shadow-xl shadow-rose-950/20 transition-all flex flex-col justify-between relative group">
            <div className="absolute top-4 right-4 bg-emerald-500/15 border border-emerald-500/30 text-emerald-400 text-[10px] font-bold px-2.5 py-0.5 rounded-full uppercase tracking-wider">
              Recommended
            </div>
            <div>
              <div className="w-12 h-12 rounded-xl bg-rose-500/15 border border-rose-500/30 flex items-center justify-center mb-4 text-rose-400">
                <Laptop className="w-6 h-6" />
              </div>
              <h3 className="text-xl font-bold text-white mb-2">Single-File Executable (.exe)</h3>
              <p className="text-xs text-slate-400 mb-6 leading-relaxed">
                Direct portable application. No installation or administrator permissions needed. 
                Double-click to start working immediately.
              </p>
              <div className="space-y-2 text-xs text-slate-300 mb-8">
                <div className="flex items-center space-x-2">
                  <CheckCircle2 className="w-4 h-4 text-emerald-400 shrink-0" />
                  <span>Version: <strong>1.0.0-beta (Latest)</strong></span>
                </div>
                <div className="flex items-center space-x-2">
                  <CheckCircle2 className="w-4 h-4 text-emerald-400 shrink-0" />
                  <span>File size: <strong>~70 MB (Self-contained)</strong></span>
                </div>
                <div className="flex items-center space-x-2">
                  <CheckCircle2 className="w-4 h-4 text-emerald-400 shrink-0" />
                  <span>Compatible with: <strong>Windows 10 & 11 (64-bit)</strong></span>
                </div>
              </div>
            </div>

            <a
              href={exeUrl}
              download="PunnyaHeartDesk.exe"
              className="w-full py-3.5 px-6 rounded-xl bg-gradient-to-r from-rose-600 via-rose-500 to-pink-600 hover:from-rose-500 hover:to-pink-500 text-white font-bold text-sm shadow-lg shadow-rose-600/30 flex items-center justify-center space-x-2.5 transition active:scale-[0.98]"
            >
              <Download className="w-4 h-4" />
              <span>Download PunnyaHeartDesk.exe</span>
            </a>
          </div>

          {/* Card 2: Portable ZIP Package */}
          <div className="bg-[#151720] border border-[#262938] hover:border-[#383D52] rounded-2xl p-6 sm:p-8 shadow-lg transition-all flex flex-col justify-between">
            <div>
              <div className="w-12 h-12 rounded-xl bg-[#1C1F2B] border border-[#2E3347] flex items-center justify-center mb-4 text-slate-300">
                <FileArchive className="w-6 h-6 text-indigo-400" />
              </div>
              <h3 className="text-xl font-bold text-white mb-2">Complete ZIP Package (.zip)</h3>
              <p className="text-xs text-slate-400 mb-6 leading-relaxed">
                Includes the executable, launcher batch script (<code className="text-slate-300">Launch_PunnyaHeartDesk.bat</code>), 
                and testing documentation in a single archive.
              </p>
              <div className="space-y-2 text-xs text-slate-300 mb-8">
                <div className="flex items-center space-x-2">
                  <CheckCircle2 className="w-4 h-4 text-indigo-400 shrink-0" />
                  <span>Version: <strong>1.0.0-beta (Full Package)</strong></span>
                </div>
                <div className="flex items-center space-x-2">
                  <CheckCircle2 className="w-4 h-4 text-indigo-400 shrink-0" />
                  <span>Compressed size: <strong>~65 MB</strong></span>
                </div>
                <div className="flex items-center space-x-2">
                  <CheckCircle2 className="w-4 h-4 text-indigo-400 shrink-0" />
                  <span>Includes: <strong>Desktop launcher batch file</strong></span>
                </div>
              </div>
            </div>

            <a
              href={zipUrl}
              download="PunnyaHeartDesk-Windows.zip"
              className="w-full py-3.5 px-6 rounded-xl bg-[#1E2230] hover:bg-[#282D40] border border-[#2F354D] text-white font-bold text-sm shadow-md flex items-center justify-center space-x-2.5 transition active:scale-[0.98]"
            >
              <Download className="w-4 h-4 text-indigo-400" />
              <span>Download ZIP Archive</span>
            </a>
          </div>
        </div>

        {/* Quick Setup Instructions */}
        <div className="bg-[#151720] border border-[#262938] rounded-2xl p-6 sm:p-8 mb-12">
          <h2 className="text-lg font-bold text-white mb-6 flex items-center space-x-2.5">
            <Info className="w-5 h-5 text-rose-400" />
            <span>How to Install and Launch (4 Simple Steps)</span>
          </h2>

          <div className="grid grid-cols-1 md:grid-cols-4 gap-4">
            <div className="bg-[#0D0E12] border border-[#202330] rounded-xl p-4">
              <div className="w-7 h-7 rounded-lg bg-rose-500/20 text-rose-400 font-bold text-xs flex items-center justify-center mb-3">
                1
              </div>
              <h4 className="text-sm font-semibold text-white mb-1">Download App</h4>
              <p className="text-xs text-slate-400">Click the download button above to save <code className="text-rose-300">PunnyaHeartDesk.exe</code> on your computer.</p>
            </div>

            <div className="bg-[#0D0E12] border border-[#202330] rounded-xl p-4">
              <div className="w-7 h-7 rounded-lg bg-rose-500/20 text-rose-400 font-bold text-xs flex items-center justify-center mb-3">
                2
              </div>
              <h4 className="text-sm font-semibold text-white mb-1">Double Click</h4>
              <p className="text-xs text-slate-400">Double click the downloaded file to run it. No complex setup or administrator rights are required.</p>
            </div>

            <div className="bg-[#0D0E12] border border-[#202330] rounded-xl p-4">
              <div className="w-7 h-7 rounded-lg bg-rose-500/20 text-rose-400 font-bold text-xs flex items-center justify-center mb-3">
                3
              </div>
              <h4 className="text-sm font-semibold text-white mb-1">SmartScreen Note</h4>
              <p className="text-xs text-slate-400">If Windows SmartScreen prompts: click <strong>More info</strong>, then click <strong>Run anyway</strong>.</p>
            </div>

            <div className="bg-[#0D0E12] border border-[#202330] rounded-xl p-4">
              <div className="w-7 h-7 rounded-lg bg-rose-500/20 text-rose-400 font-bold text-xs flex items-center justify-center mb-3">
                4
              </div>
              <h4 className="text-sm font-semibold text-white mb-1">Login & Auto Clock-In</h4>
              <p className="text-xs text-slate-400">Sign in with your staff credentials. You will be <strong>automatically clocked in</strong> for your shift!</p>
            </div>
          </div>
        </div>

        {/* Workstation Features & Security */}
        <div className="border-t border-[#262938] pt-10">
          <div className="text-center mb-8">
            <h3 className="text-xl font-bold text-white mb-2">Built-in Workstation Features</h3>
            <p className="text-xs text-slate-400">Engineered for security, transparency, and seamless workforce tracking.</p>
          </div>

          <div className="grid grid-cols-1 sm:grid-cols-3 gap-5">
            <div className="bg-[#151720]/60 border border-[#262938] p-5 rounded-xl flex items-start space-x-3.5">
              <div className="w-9 h-9 rounded-lg bg-rose-500/15 text-rose-400 flex items-center justify-center shrink-0 mt-0.5">
                <Clock className="w-5 h-5" />
              </div>
              <div>
                <h5 className="text-sm font-bold text-white mb-1">Instant Auto Clock-In</h5>
                <p className="text-xs text-slate-400">Automatically logs shift start times and attributes active vs idle time accurately down to the second.</p>
              </div>
            </div>

            <div className="bg-[#151720]/60 border border-[#262938] p-5 rounded-xl flex items-start space-x-3.5">
              <div className="w-9 h-9 rounded-lg bg-emerald-500/15 text-emerald-400 flex items-center justify-center shrink-0 mt-0.5">
                <ShieldCheck className="w-5 h-5" />
              </div>
              <div>
                <h5 className="text-sm font-bold text-white mb-1">Offline & Tamper-Proof</h5>
                <p className="text-xs text-slate-400">Stores heartbeat telemetry safely in an encrypted local database when internet disconnects, then auto-syncs when online.</p>
              </div>
            </div>

            <div className="bg-[#151720]/60 border border-[#262938] p-5 rounded-xl flex items-start space-x-3.5">
              <div className="w-9 h-9 rounded-lg bg-indigo-500/15 text-indigo-400 flex items-center justify-center shrink-0 mt-0.5">
                <Key className="w-5 h-5" />
              </div>
              <div>
                <h5 className="text-sm font-bold text-white mb-1">WFH / Office Auto-Detect</h5>
                <p className="text-xs text-slate-400">Automatically tags your workstation machine name and identifies Work-From-Home or In-Office status.</p>
              </div>
            </div>
          </div>
        </div>
      </main>

      {/* Footer */}
      <footer className="border-t border-[#262938] bg-[#0A0B0E] py-6 px-4 text-center text-xs text-slate-500">
        <p>© 2026 Punnya Heart Foundation. All rights reserved. • Enterprise Work Management System</p>
      </footer>
    </div>
  );
}
