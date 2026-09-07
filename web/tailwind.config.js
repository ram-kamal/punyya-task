/** @type {import('tailwindcss').Config} */
module.exports = {
  content: [
    './src/pages/**/*.{js,ts,jsx,tsx,mdx}',
    './src/components/**/*.{js,ts,jsx,tsx,mdx}',
    './src/app/**/*.{js,ts,jsx,tsx,mdx}',
  ],
  theme: {
    extend: {
      colors: {
        darkBg: '#090D16',
        surface: '#131B2E',
        surfaceHover: '#1B263F',
        cardBorder: '#233252',
        accentBlue: '#2563EB',
        accentGreen: '#10B981',
        accentAmber: '#F59E0B',
        accentRed: '#EF4444',
      },
    },
  },
  plugins: [],
}
