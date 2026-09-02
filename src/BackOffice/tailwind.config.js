/** @type {import('tailwindcss').Config} */
// Miroir de src/Website/tailwind.config.js : le BackOffice et le Website partagent
// désormais le même design system (mêmes couleurs, mêmes familles de police, même
// gouttière de bande). Toute évolution de la palette doit être répercutée des deux
// côtés — et dans les _colors.scss / _typography.scss correspondants.
module.exports = {
  content: [
    "./src/**/*.{html,ts}",
    "../SharedUi/src/**/*.{html,ts}",
  ],
  theme: {
    extend: {
      // Gouttière des bandes de page : la valeur normale tant que l'écran est étroit,
      // puis la moitié du débordement dès que l'écran dépasse --vpd-shell-max, ce qui
      // borne et centre le contenu sans toucher au fond de la bande.
      // Variables définies dans src/styles.scss.
      spacing: {
        shell: "max(var(--vpd-shell-gutter), (100% - var(--vpd-shell-max)) / 2)",
      },
      maxWidth: {
        shell: "var(--vpd-shell-max)",
      },
      fontFamily: {
        serif: ["Newsreader", "Georgia", "serif"],
        sans: ["Libre Franklin", "system-ui", "sans-serif"],
        mono: ["IBM Plex Mono", "ui-monospace", "monospace"],
      },
      colors: {
        paper: "#f7fbfe",
        "paper-2": "#e9f4fb",
        ink: "#041d30",
        "ink-2": "#072b45",
        slate: "#33536e",
        "slate-2": "#4e6c84",
        "slate-3": "#6d8ba2",
        mist: "#9dc2da",
        "mist-2": "#dcecf7",
        blue: "#0c6ea6",
        "blue-2": "#1497d6",
        cyan: "#7fd8f5",
        orange: "#f0801c",
        "orange-2": "#f9a93c",
        "orange-3": "#dc6412",
        line: "#d9e9f4",
        "line-2": "#e2eef7",
        success: "#28A745",

        // Propres au BackOffice (absents du Website, qui n'a ni saisie ni suppression) :
        // état d'erreur de formulaire et actions destructrices.
        danger: "#c0392b",
        "danger-soft": "#fbeae7",

        // @vpd/ui (src/SharedUi, partagée avec le Website) référence encore ces noms de
        // couleur dans ses propres styles — vpd-image, vpd-event-card, vpd-product-card,
        // vpd-product-list, vpd-button. Le Website les fait déjà pointer vers la nouvelle
        // palette (voir son tailwind.config.js) : on reprend exactement les mêmes valeurs
        // ici pour que ces composants partagés s'affichent à l'identique dans les deux
        // apps, sans toucher à SharedUi. À retirer si ce paquet est un jour migré vers des
        // classes `ink-2` / `blue-2` / etc. directement.
        "primary-color": "#072b45",
        "secondary-color": "#1497d6",
        "tertiary-color": "#f0801c",
        "text-color": "#33536e",
      },
      backgroundImage: {
        "vpd-brand": "linear-gradient(90deg, #1497d6, #7fd8f5 50%, #f0801c)",
        // Même filet de marque, tourné à la verticale : utilisé par le chapeau de
        // section en orientation "left".
        "vpd-brand-v": "linear-gradient(180deg, #1497d6, #7fd8f5 55%, #f0801c)",
        "vpd-hero": "linear-gradient(205deg, #7fd8f5 0%, #1497d6 26%, #0c6ea6 52%, #062a44 100%)",
      },
      animation: {
        "vpd-float": "vpdFloat 8s ease-in-out infinite",
      },
      keyframes: {
        vpdFloat: {
          "0%, 100%": { transform: "translateY(0) rotate(var(--r, 0deg))" },
          "50%": { transform: "translateY(-9px) rotate(var(--r, 0deg))" },
        },
      },
    },
  },
  plugins: [],
}
