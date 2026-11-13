# DESIGN.md — Apple Design System Specification

## 1. Core Philosophy & Visual Aesthetic
- **Content-First Minimalism:** Interfaces exist to elevate the content or hardware, never to compete with it. Eliminate visual clutter, heavy borders, and unnecessary ornamentation.
- **Alternating High-Contrast Rhythm:** Alternate between pure white/light-gray editorial sections and pure black (`#000000`) immersive sections to create dramatic pacing and clear visual separation.
- **Generous Negative Space:** Let typography and product imagery breathe. Default to large vertical section padding (`80px`–`120px` on desktop, `48px`–`64px` on mobile).
- **Translucent Depth:** Use frosted glass materials (`backdrop-filter: blur(20px) saturate(180%)`) for sticky navigation bars, floating controls, and modals so background color subtly bleeds through.

---

## 2. Color Palette & Tokens

### Light Mode (Default Editorial Surface)
- **Primary Background:** `#ffffff` (Pure White)
- **Secondary Surface / Card Fill:** `#f5f5f7` (Apple Parchment Gray)
- **Tertiary Surface / Hover Fill:** `#e8e8ed`
- **Primary Text:** `#1d1d1f` (Near Black — never use harsh `#000000` for body copy on white)
- **Secondary Text / Captions:** `#6e6e73` (Muted Slate Gray)
- **Tertiary / Disabled Text:** `#86868b`
- **Hairline Divider / Border:** `rgba(0, 0, 0, 0.08)` or `#d2d2d7`

### Dark Mode (Hero / Pro Showcase Surface)
- **Primary Background:** `#000000` (True OLED Black)
- **Secondary Surface / Card Fill:** `#161617` or `#1d1d1f`
- **Tertiary Surface / Elevated Card:** `#2c2c2e`
- **Primary Text:** `#f5f5f7` (Off-White)
- **Secondary Text / Captions:** `#86868b`
- **Hairline Divider / Border:** `rgba(255, 255, 255, 0.12)` or `#424245`

### Accent & Interactive Colors
- **Action Blue (Primary CTA & Links):** `#0066cc` (Light Mode) / `#2997ff` (Dark Mode)
- **Action Blue Hover:** `#0077ed` (Light Mode) / `#40a9ff` (Dark Mode)
- **Destructive / Alert Red:** `#ff3b30`
- **Success Green:** `#34c759`
- **Warning Amber:** `#ff9f0a`

---

## 3. Typography & Hierarchy

### Font Stack
```css
font-family: -apple-system, BlinkMacSystemFont, "SF Pro Display", "SF Pro Text", "Helvetica Neue", Arial, sans-serif;