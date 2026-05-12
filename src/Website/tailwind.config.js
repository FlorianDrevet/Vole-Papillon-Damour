/** @type {import('tailwindcss').Config} */
module.exports = {
  content: [
    "./src/**/*.{html,ts}",
  ],
  theme: {
    fontFamily: {
      'caveatbrush': ["CaveatBrush"],
      'dancing': ["DancingScript-Regular"],
    },
    colors: {
      'primary-color': '#012f5f',
      'secondary-color': '#1ACDF8',
      'tertiary-color': '#F67700',
      'text-color': '#37628A',
      'background-color': '#E9F3FF',
      'white': '#FFFFFF',
      'gold': '#e3bb71',
      'success': '#28a745',
    },
    extend: {},
  },
  plugins: [],
}

