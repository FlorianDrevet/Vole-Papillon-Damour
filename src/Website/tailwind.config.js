/** @type {import('tailwindcss').Config} */
module.exports = {
  content: [
    "./src/**/*.{html,ts}",
    "../SharedUi/src/**/*.{html,ts}",
  ],
  theme: {
    extend: {
      fontFamily: {
        'caveatbrush': ["CaveatBrush", "Segoe UI", "cursive"],
        'dancing': ["DancingScript-Regular", "CaveatBrush", "cursive"],
      },
      colors: {
        'primary-color': 'rgb(var(--vpd-primary) / <alpha-value>)',
        'secondary-color': 'rgb(var(--vpd-secondary) / <alpha-value>)',
        'tertiary-color': 'rgb(var(--vpd-tertiary) / <alpha-value>)',
        'text-color': 'rgb(var(--vpd-text) / <alpha-value>)',
        'background-color': 'rgb(var(--vpd-background) / <alpha-value>)',
        'surface': 'rgb(var(--vpd-surface) / <alpha-value>)',
        'surface-soft': 'rgb(var(--vpd-surface-soft) / <alpha-value>)',
        'surface-strong': 'rgb(var(--vpd-surface-strong) / <alpha-value>)',
        'ink': 'rgb(var(--vpd-ink) / <alpha-value>)',
        'ink-soft': 'rgb(var(--vpd-ink-soft) / <alpha-value>)',
        'line': 'rgb(var(--vpd-line) / <alpha-value>)',
        'gold': '#E3BB71',
        'success': '#28A745',
      },
      boxShadow: {
        'vpd-soft': '0 18px 40px -24px rgb(var(--vpd-shadow) / 0.45)',
        'vpd-panel': '0 28px 80px -38px rgb(var(--vpd-shadow) / 0.52)',
        'vpd-float': '0 22px 48px -28px rgb(var(--vpd-shadow) / 0.38)',
      },
      borderRadius: {
        'vpd': '1.5rem',
        'vpd-xl': '2rem',
        'vpd-2xl': '2.5rem',
      },
      backgroundImage: {
        'vpd-hero-glow': 'radial-gradient(circle at 12% 18%, rgb(var(--vpd-secondary) / 0.22), transparent 34%), radial-gradient(circle at 88% 12%, rgb(var(--vpd-tertiary) / 0.18), transparent 26%), linear-gradient(180deg, rgb(var(--vpd-background) / 0.96), rgb(var(--vpd-surface-soft) / 0.98))',
      },
    },
  },
  plugins: [],
}

