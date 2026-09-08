/** @type {import('tailwindcss').Config} */
module.exports = {
  darkMode: 'class',
  content: [
    './src/pages/**/*.{js,ts,jsx,tsx,mdx}',
    './src/components/**/*.{js,ts,jsx,tsx,mdx}',
    './src/app/**/*.{js,ts,jsx,tsx,mdx}',
  ],
  theme: {
    extend: {
      colors: {
        darkBg: '#0D0E12',
        surface: '#151720',
        surfaceHover: '#1C1F2B',
        cardBorder: '#262938',
        accentRose: '#E11D48',
        accentRoseHover: '#BE123C',
        accentGreen: '#10B981',
        accentAmber: '#F59E0B',
        accentPurple: '#8B5CF6',
      },

    },
  },
  plugins: [],
};
