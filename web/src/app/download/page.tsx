'use client';

import React, { useState, useEffect } from 'react';
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
  Info,
  Sparkles,
  Command,
  Apple,
  Smartphone,
  Wifi
} from 'lucide-react';
import { getAppVersion } from '@/lib/api';

export default function DownloadPage() {
  const [selectedOS, setSelectedOS] = useState<'mac' | 'windows' | 'android'>('windows');
  const [appVersion, setAppVersion] = useState<string>('1.0.2');

  useEffect(() => {
    if (typeof window !== 'undefined') {
      const isApple = /Mac|iPod|iPhone|iPad/i.test(navigator.platform) || /Macintosh/i.test(navigator.userAgent);
      if (isApple) {
        setSelectedOS('mac');
      }
    }

    getAppVersion()
      .then(info => {
        if (info?.latestVersion) {
          setAppVersion(info.latestVersion);
        }
      })
      .catch(() => {});
  }, []);

  const macArmUrl = '/task/downloads/PunnyaHeartDesk-macOS-AppleSilicon.zip';
  const macIntelUrl = '/task/downloads/PunnyaHeartDesk-macOS-Intel.zip';
  const setupUrl = '/task/downloads/PunnyaHeartDesk-Setup.exe';
  const exeUrl = '/task/downloads/PunnyaHeartDesk.exe';
  const zipUrl = '/task/downloads/PunnyaHeartDesk-Windows.zip';
  const apkUrl = '/task/downloads/PunnyaAttendance.apk';

  return (
    <div className="min-h-screen bg-[#0D0E12] text-slate-100 flex flex-col font-sans antialiased selection:bg-rose-500 selection:text-white">
      {/* Background ambient glows */}
      <div className="fixed top-0 left-1/2 -translate-x-1/2 w-full max-w-7xl h-96 bg-gradient-to-b from-rose-600/15 via-rose-500/5 to-transparent blur-3xl pointer-events-none -z-10" />
      <div className="fixed top-40 right-10 w-80 h-80 bg-indigo-600/10 blur-3xl pointer-events-none -z-10" />

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
              <p className="text-xs text-slate-400">Workstation Telemetry &amp; Task Management System</p>
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
        <div className="text-center max-w-3xl mx-auto mb-10">
          <div className="inline-flex items-center space-x-2 px-3.5 py-1 rounded-full bg-rose-500/10 border border-rose-500/25 text-rose-400 text-xs font-semibold mb-4 backdrop-blur-sm">
            <Sparkles className="w-3.5 h-3.5" />
            <span>Cross-Platform Workstation Desk • Windows &amp; macOS</span>
          </div>
          <h1 className="text-3xl sm:text-5xl font-black text-white tracking-tight leading-tight mb-4">
            Download <span className="bg-gradient-to-r from-rose-400 via-rose-300 to-pink-400 bg-clip-text text-transparent">Punnya Heart Desk</span>
          </h1>
          <p className="text-slate-300 text-sm sm:text-base leading-relaxed">
            The dedicated desktop application for staff. Enables automatic shift clock-in, 
            task execution, deliverable proof gates, and tamper-proof offline activity telemetry.
          </p>

          {/* OS Switcher Segmented Control */}
          <div className="inline-flex items-center p-1.5 rounded-2xl bg-[#151722] border border-[#2B3044] mt-8 shadow-xl">
            <button
              onClick={() => setSelectedOS('mac')}
              className={`flex items-center space-x-2.5 px-6 py-2.5 rounded-xl font-bold text-xs sm:text-sm transition-all ${
                selectedOS === 'mac'
                  ? 'bg-gradient-to-r from-rose-600 to-pink-600 text-white shadow-lg shadow-rose-600/30'
                  : 'text-slate-400 hover:text-slate-200 hover:bg-[#1E2232]'
              }`}
            >
              <Apple className="w-4 h-4" />
              <span>macOS (Apple Silicon &amp; Intel)</span>
            </button>
            <button
              onClick={() => setSelectedOS('windows')}
              className={`flex items-center space-x-2.5 px-6 py-2.5 rounded-xl font-bold text-xs sm:text-sm transition-all ${
                selectedOS === 'windows'
                  ? 'bg-gradient-to-r from-rose-600 to-pink-600 text-white shadow-lg shadow-rose-600/30'
                  : 'text-slate-400 hover:text-slate-200 hover:bg-[#1E2232]'
              }`}
            >
              <Laptop className="w-4 h-4" />
              <span>Windows (10 &amp; 11)</span>
            </button>
          </div>
        </div>

        {/* macOS Section */}
        {selectedOS === 'mac' && (
          <div>
            {/* macOS Golden Gate Glass Card */}
            <div className="mb-10">
              <div className="bg-[#141824]/85 backdrop-blur-xl border-2 border-rose-500/60 hover:border-rose-500 rounded-3xl p-6 sm:p-10 shadow-2xl shadow-rose-950/40 transition-all relative overflow-hidden">
                <div className="absolute top-0 right-0 bg-gradient-to-l from-rose-600 to-pink-600 text-white text-[11px] font-extrabold px-4 py-1 rounded-bl-2xl uppercase tracking-wider shadow-md">
                  ★ macOS Edition (Golden Gate Glass)
                </div>

                <div className="flex flex-col lg:flex-row lg:items-center lg:justify-between gap-8">
                  <div className="space-y-4 max-w-xl">
                    <div className="flex items-center space-x-3.5">
                      <div className="w-14 h-14 rounded-2xl bg-gradient-to-br from-rose-500/20 to-indigo-500/20 border border-rose-500/30 flex items-center justify-center text-rose-400 shrink-0 shadow-inner">
                        <Apple className="w-7 h-7" />
                      </div>
                      <div>
                        <h3 className="text-2xl font-black text-white tracking-tight">Punnya Heart Desk for Mac</h3>
                        <p className="text-xs text-rose-300 font-medium">Native Apple Silicon (M1/M2/M3/M4) &amp; Intel Macs</p>
                      </div>
                    </div>

                    <p className="text-sm text-slate-300 leading-relaxed">
                      Engineered with a native <strong>macOS Golden Gate Glass</strong> design, featuring liquid acrylic translucency, 
                      CoreGraphics shift telemetry, offline SQLite cryptographic buffer, and automatic shift clock-in.
                    </p>

                    <div className="grid grid-cols-1 sm:grid-cols-2 gap-2.5 text-xs text-slate-300 pt-2">
                      <div className="flex items-center space-x-2">
                        <CheckCircle2 className="w-4 h-4 text-emerald-400 shrink-0" />
                        <span>macOS Glass &amp; Dark Vibrancy</span>
                      </div>
                      <div className="flex items-center space-x-2">
                        <CheckCircle2 className="w-4 h-4 text-emerald-400 shrink-0" />
                        <span>CoreGraphics Idle Tracking</span>
                      </div>
                      <div className="flex items-center space-x-2">
                        <CheckCircle2 className="w-4 h-4 text-emerald-400 shrink-0" />
                        <span>No .NET or Runtime Install Required</span>
                      </div>
                      <div className="flex items-center space-x-2">
                        <CheckCircle2 className="w-4 h-4 text-emerald-400 shrink-0" />
                        <span>Tamper-Proof SQLite Offline Buffer</span>
                      </div>
                    </div>
                  </div>

                  {/* Mac Download Buttons */}
                  <div className="shrink-0 flex flex-col items-center lg:items-end justify-center space-y-3 min-w-[300px]">
                    <a
                      href={macArmUrl}
                      download="PunnyaHeartDesk-macOS-AppleSilicon.zip"
                      className="w-full py-4 px-6 rounded-2xl bg-gradient-to-r from-rose-600 via-rose-500 to-pink-600 hover:from-rose-500 hover:to-pink-500 text-white font-black text-sm shadow-xl shadow-rose-600/35 flex items-center justify-center space-x-2.5 transition active:scale-[0.98] border border-rose-400/30"
                    >
                      <Download className="w-5 h-5" />
                      <div className="text-left">
                        <div>Download for Apple Silicon</div>
                        <div className="text-[10px] font-normal text-rose-200">M1, M2, M3, M4 Macs (Recommended • ~41 MB)</div>
                      </div>
                    </a>

                    <a
                      href={macIntelUrl}
                      download="PunnyaHeartDesk-macOS-Intel.zip"
                      className="w-full py-3 px-6 rounded-xl bg-[#1D2130] hover:bg-[#272C40] border border-[#363C54] text-slate-200 hover:text-white font-bold text-xs flex items-center justify-center space-x-2.5 transition active:scale-[0.98]"
                    >
                      <Download className="w-4 h-4 text-slate-400" />
                      <div className="text-left">
                        <div>Download for Intel Mac (x64)</div>
                        <div className="text-[10px] font-normal text-slate-400">For older Intel-based Macs (~43 MB)</div>
                      </div>
                    </a>
                  </div>
                </div>
              </div>
            </div>

            {/* Quick 3-Step macOS Setup Instructions */}
            <div className="bg-[#151720]/80 border border-[#262938] rounded-2xl p-6 sm:p-8 mb-14">
              <div className="flex items-center space-x-3 mb-6">
                <div className="w-8 h-8 rounded-lg bg-indigo-500/15 text-indigo-400 flex items-center justify-center font-bold text-xs">
                  Mac
                </div>
                <h4 className="text-base font-bold text-white">2-Minute Quick Setup Guide for Mac Users</h4>
              </div>

              <div className="grid grid-cols-1 md:grid-cols-3 gap-5">
                <div className="bg-[#1B1E2B] border border-[#2B3044] p-4 rounded-xl space-y-2">
                  <div className="w-6 h-6 rounded-full bg-rose-500/20 text-rose-400 flex items-center justify-center text-xs font-bold">1</div>
                  <h5 className="text-sm font-bold text-white">Unzip Archive</h5>
                  <p className="text-xs text-slate-400 leading-relaxed">
                    Double-click the downloaded <code className="text-slate-200">PunnyaHeartDesk-macOS-AppleSilicon.zip</code>. 
                    It expands to produce <strong className="text-slate-200">PunnyaHeartDesk.app</strong>.
                  </p>
                </div>

                <div className="bg-[#1B1E2B] border border-[#2B3044] p-4 rounded-xl space-y-2">
                  <div className="w-6 h-6 rounded-full bg-rose-500/20 text-rose-400 flex items-center justify-center text-xs font-bold">2</div>
                  <h5 className="text-sm font-bold text-white">Move to Applications</h5>
                  <p className="text-xs text-slate-400 leading-relaxed">
                    Drag <strong className="text-slate-200">PunnyaHeartDesk.app</strong> into your Mac&apos;s <strong className="text-slate-200">Applications</strong> folder in Finder so it can run smoothly and be accessed via Launchpad.
                  </p>
                </div>

                <div className="bg-[#1B1E2B] border border-[#2B3044] p-4 rounded-xl space-y-2">
                  <div className="w-6 h-6 rounded-full bg-rose-500/20 text-rose-400 flex items-center justify-center text-xs font-bold">3</div>
                  <h5 className="text-sm font-bold text-white">First Launch &amp; Permissions</h5>
                  <p className="text-xs text-slate-400 leading-relaxed">
                    Right-click the app $\rightarrow$ click <strong className="text-emerald-400">Open</strong> (or go to System Settings $\rightarrow$ Privacy &amp; Security $\rightarrow$ <em>Open Anyway</em>). Grant <strong>Accessibility</strong> access to enable shift logging!
                  </p>
                </div>
              </div>
            </div>
          </div>
        )}

        {/* Windows Section */}
        {selectedOS === 'windows' && (
          <div>
            {/* Featured Primary Card: Windows Installer Setup Wizard */}
            <div className="mb-10">
              <div className="bg-[#151720] border-2 border-rose-500/60 hover:border-rose-500 rounded-3xl p-6 sm:p-10 shadow-2xl shadow-rose-950/30 transition-all relative overflow-hidden">
                <div className="absolute top-0 right-0 bg-gradient-to-l from-rose-600 to-pink-600 text-white text-[11px] font-extrabold px-4 py-1 rounded-bl-2xl uppercase tracking-wider shadow-md">
                  ★ Official Windows Installer (Recommended)
                </div>

                <div className="flex flex-col md:flex-row md:items-center md:justify-between gap-8">
                  <div className="space-y-4 max-w-xl">
                    <div className="flex items-center space-x-3">
                      <div className="w-14 h-14 rounded-2xl bg-rose-500/15 border border-rose-500/30 flex items-center justify-center text-rose-400 shrink-0">
                        <Laptop className="w-7 h-7" />
                      </div>
                      <div>
                        <h3 className="text-2xl font-black text-white tracking-tight">Windows Setup Wizard (.exe)</h3>
                        <p className="text-xs text-rose-300/80 font-medium">Windows 11 Fluent Mica Glass UI • Windows 10 &amp; 11 Ready</p>
                      </div>
                    </div>

                    <p className="text-sm text-slate-300 leading-relaxed">
                      Features modern Windows 11 Fluent Mica Glass materials, live elapsed shift clock HUD, Win32 telemetry hooks, and automatic boot configuration.
                    </p>

                    <div className="grid grid-cols-1 sm:grid-cols-2 gap-2.5 text-xs text-slate-300 pt-2">
                      <div className="flex items-center space-x-2">
                        <CheckCircle2 className="w-4 h-4 text-emerald-400 shrink-0" />
                        <span>Windows 11 Fluent Mica Translucency</span>
                      </div>
                      <div className="flex items-center space-x-2">
                        <CheckCircle2 className="w-4 h-4 text-emerald-400 shrink-0" />
                        <span>Live Elapsed Shift Clock HUD</span>
                      </div>
                      <div className="flex items-center space-x-2">
                        <CheckCircle2 className="w-4 h-4 text-emerald-400 shrink-0" />
                        <span>No Admin Rights Required</span>
                      </div>
                      <div className="flex items-center space-x-2">
                        <CheckCircle2 className="w-4 h-4 text-emerald-400 shrink-0" />
                        <span>Built-in Windows Uninstaller</span>
                      </div>
                    </div>
                  </div>

                  <div className="shrink-0 flex flex-col items-center md:items-end justify-center space-y-3 min-w-[280px]">
                    <a
                      href={exeUrl}
                      download="PunnyaHeartDesk.exe"
                      className="w-full py-4 px-8 rounded-2xl bg-gradient-to-r from-rose-600 via-rose-500 to-pink-600 hover:from-rose-500 hover:to-pink-500 text-white font-black text-base shadow-xl shadow-rose-600/35 flex items-center justify-center space-x-3 transition active:scale-[0.98] border border-rose-400/30"
                    >
                      <Download className="w-5 h-5" />
                      <span>Download Windows App (v{appVersion})</span>
                    </a>
                    <div className="text-[11px] text-slate-400 text-center md:text-right space-y-0.5">
                      <div>File: <strong className="text-slate-200">PunnyaHeartDesk.exe</strong></div>
                      <div>Version: <strong className="text-emerald-400">v{appVersion} (Latest Release)</strong> • Size: <strong className="text-slate-200">~46 MB</strong></div>
                    </div>
                  </div>
                </div>
              </div>
            </div>

            {/* Secondary Download Cards (Portable Alternatives) */}
            <div className="mb-14">
              <div className="flex items-center space-x-3 mb-4">
                <h4 className="text-xs font-bold uppercase tracking-wider text-slate-400">Alternative Portable Packages</h4>
                <div className="h-px bg-[#262938] flex-1" />
              </div>

              <div className="grid grid-cols-1 md:grid-cols-2 gap-5">
                {/* Standalone Portable Exe */}
                <div className="bg-[#151720] border border-[#262938] hover:border-[#383D52] rounded-2xl p-6 transition flex flex-col justify-between">
                  <div>
                    <div className="flex items-center justify-between mb-3">
                      <h5 className="text-base font-bold text-white">Standalone Portable Executable</h5>
                      <span className="text-[10px] bg-[#222634] text-slate-300 font-semibold px-2 py-0.5 rounded">.EXE</span>
                    </div>
                    <p className="text-xs text-slate-400 mb-4 leading-relaxed">
                      Single standalone binary with Windows 11 Fluent Mica design. Runs directly without installation wizard.
                    </p>
                    <div className="text-xs text-slate-400 space-y-1 mb-6">
                      <div>Size: <strong className="text-slate-300">~46 MB</strong> (Self-contained, single file)</div>
                    </div>
                  </div>
                  <a
                    href={exeUrl}
                    download="PunnyaHeartDesk.exe"
                    className="w-full py-2.5 px-4 rounded-xl bg-[#1C1F2B] hover:bg-[#262A3B] border border-[#32364A] text-slate-200 hover:text-white font-bold text-xs flex items-center justify-center space-x-2 transition"
                  >
                    <Download className="w-3.5 h-3.5 text-slate-400" />
                    <span>Download PunnyaHeartDesk.exe (Portable)</span>
                  </a>
                </div>

                {/* Portable ZIP Archive */}
                <div className="bg-[#151720] border border-[#262938] hover:border-[#383D52] rounded-2xl p-6 transition flex flex-col justify-between">
                  <div>
                    <div className="flex items-center justify-between mb-3">
                      <h5 className="text-base font-bold text-white">Portable ZIP Archive</h5>
                      <span className="text-[10px] bg-[#222634] text-slate-300 font-semibold px-2 py-0.5 rounded">.ZIP</span>
                    </div>
                    <p className="text-xs text-slate-400 mb-4 leading-relaxed">
                      Compressed archive containing the executable, launcher batch script, and documentation.
                    </p>
                    <div className="text-xs text-slate-400 space-y-1 mb-6">
                      <div>Size: <strong className="text-slate-300">~41 MB</strong> (Compressed archive)</div>
                    </div>
                  </div>
                  <a
                    href={zipUrl}
                    download="PunnyaHeartDesk-Windows.zip"
                    className="w-full py-2.5 px-4 rounded-xl bg-[#1C1F2B] hover:bg-[#262A3B] border border-[#32364A] text-slate-200 hover:text-white font-bold text-xs flex items-center justify-center space-x-2 transition"
                  >
                    <Download className="w-3.5 h-3.5 text-indigo-400" />
                    <span>Download ZIP Package</span>
                  </a>
                </div>
              </div>
            </div>
          </div>
        )}

        {/* Android Section */}
        {selectedOS === 'android' && (
          <div>
            <div className="mb-10">
              <div className="bg-[#151720] border-2 border-rose-500/60 hover:border-rose-500 rounded-3xl p-6 sm:p-10 shadow-2xl shadow-rose-950/30 transition-all relative overflow-hidden">
                <div className="absolute top-0 right-0 bg-gradient-to-l from-rose-600 to-pink-600 text-white text-[11px] font-extrabold px-4 py-1 rounded-bl-2xl uppercase tracking-wider shadow-md">
                  ★ Official Android APK
                </div>

                <div className="flex flex-col md:flex-row md:items-center md:justify-between gap-8">
                  <div className="space-y-4 max-w-xl">
                    <div className="flex items-center space-x-3">
                      <div className="w-14 h-14 rounded-2xl bg-rose-500/15 border border-rose-500/30 flex items-center justify-center text-rose-400 shrink-0">
                        <Smartphone className="w-7 h-7" />
                      </div>
                      <div>
                        <h3 className="text-2xl font-black text-white tracking-tight">Punnya Attendance APK</h3>
                        <p className="text-xs text-rose-300/80 font-medium">Auto Office Wi-Fi Shift Detection • GPS Geo-Fence • Tasks &amp; Hours</p>
                      </div>
                    </div>

                    <p className="text-sm text-slate-300 leading-relaxed">
                      Dedicated mobile attendance companion for clinic and hospital staff. Automatically marks shift login upon entering and connecting to the office Wi-Fi, features a drop-in clock-out button, tracks total daily hours worked, and displays assigned tasks.
                    </p>

                    <div className="grid grid-cols-1 sm:grid-cols-2 gap-2.5 text-xs text-slate-300 pt-2">
                      <div className="flex items-center space-x-2">
                        <CheckCircle2 className="w-4 h-4 text-emerald-400 shrink-0" />
                        <span>Auto Clock-in via Office Wi-Fi</span>
                      </div>
                      <div className="flex items-center space-x-2">
                        <CheckCircle2 className="w-4 h-4 text-emerald-400 shrink-0" />
                        <span>Drop-in 1-Tap Clock-Out</span>
                      </div>
                      <div className="flex items-center space-x-2">
                        <CheckCircle2 className="w-4 h-4 text-emerald-400 shrink-0" />
                        <span>Live Shift Timer &amp; Worked Hours</span>
                      </div>
                      <div className="flex items-center space-x-2">
                        <CheckCircle2 className="w-4 h-4 text-emerald-400 shrink-0" />
                        <span>Assigned Tasks &amp; Status Updates</span>
                      </div>
                    </div>
                  </div>

                  <div className="shrink-0 flex flex-col items-center md:items-end justify-center space-y-3 min-w-[280px]">
                    <a
                      href={apkUrl}
                      download="PunnyaAttendance.apk"
                      className="w-full py-4 px-8 rounded-2xl bg-gradient-to-r from-rose-600 via-rose-500 to-pink-600 hover:from-rose-500 hover:to-pink-500 text-white font-black text-base shadow-xl shadow-rose-600/35 flex items-center justify-center space-x-3 transition active:scale-[0.98] border border-rose-400/30"
                    >
                      <Download className="w-5 h-5" />
                      <span>Download Android APK</span>
                    </a>
                    <div className="text-[11px] text-slate-400 text-center md:text-right space-y-0.5">
                      <div>File: <strong className="text-slate-200">PunnyaAttendance.apk</strong></div>
                      <div>Compatibility: <strong className="text-emerald-400">Android 7.0 to Android 15</strong> • Size: <strong className="text-slate-200">~5.6 MB</strong></div>
                    </div>
                  </div>
                </div>
              </div>
            </div>

            {/* Android 3-Step Setup Guide */}
            <div className="bg-[#151720]/80 border border-[#262938] rounded-2xl p-6 sm:p-8 mb-14">
              <div className="flex items-center space-x-3 mb-6">
                <div className="w-8 h-8 rounded-lg bg-rose-500/15 text-rose-400 flex items-center justify-center font-bold text-xs">
                  APK
                </div>
                <h4 className="text-base font-bold text-white">How to Install on Your Android Phone</h4>
              </div>

              <div className="grid grid-cols-1 md:grid-cols-3 gap-5">
                <div className="bg-[#1B1E2B] border border-[#2B3044] p-4 rounded-xl space-y-2">
                  <div className="w-6 h-6 rounded-full bg-rose-500/20 text-rose-400 flex items-center justify-center text-xs font-bold">1</div>
                  <h5 className="text-sm font-bold text-white">Download APK</h5>
                  <p className="text-xs text-slate-400 leading-relaxed">
                    Tap the Download button above on your Android phone to save <code className="text-slate-200">PunnyaAttendance.apk</code>.
                  </p>
                </div>

                <div className="bg-[#1B1E2B] border border-[#2B3044] p-4 rounded-xl space-y-2">
                  <div className="w-6 h-6 rounded-full bg-rose-500/20 text-rose-400 flex items-center justify-center text-xs font-bold">2</div>
                  <h5 className="text-sm font-bold text-white">Allow Install</h5>
                  <p className="text-xs text-slate-400 leading-relaxed">
                    Tap the downloaded notification. If prompted, toggle <em>&quot;Allow from this source&quot;</em> in your browser settings.
                  </p>
                </div>

                <div className="bg-[#1B1E2B] border border-[#2B3044] p-4 rounded-xl space-y-2">
                  <div className="w-6 h-6 rounded-full bg-rose-500/20 text-rose-400 flex items-center justify-center text-xs font-bold">3</div>
                  <h5 className="text-sm font-bold text-white">Login &amp; Connect</h5>
                  <p className="text-xs text-slate-400 leading-relaxed">
                    Open the app, sign in with your Saaol credentials, and connect to the clinic Wi-Fi. It will automatically detect your shift!
                  </p>
                </div>
              </div>
            </div>
          </div>
        )}

        {/* Feature Highlights Section */}
        <div className="border-t border-[#262938] pt-12">
          <div className="text-center mb-8">
            <h3 className="text-xl font-bold text-white mb-2">Built-in Workstation Features</h3>
            <p className="text-xs text-slate-400">Engineered for security, transparency, and seamless workforce tracking across macOS and Windows.</p>
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
                <h5 className="text-sm font-bold text-white mb-1">Offline &amp; Tamper-Proof</h5>
                <p className="text-xs text-slate-400">Stores heartbeat telemetry in an encrypted SHA-256 chained SQLite buffer when disconnected, then auto-syncs when online.</p>
              </div>
            </div>

            <div className="bg-[#151720]/60 border border-[#262938] p-5 rounded-xl flex items-start space-x-3.5">
              <div className="w-9 h-9 rounded-lg bg-indigo-500/15 text-indigo-400 flex items-center justify-center shrink-0 mt-0.5">
                <Key className="w-5 h-5" />
              </div>
              <div>
                <h5 className="text-sm font-bold text-white mb-1">WFH / Office Auto-Detect</h5>
                <p className="text-xs text-slate-400">Automatically tags workstation machine name and identifies Work-From-Home or In-Office status.</p>
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
