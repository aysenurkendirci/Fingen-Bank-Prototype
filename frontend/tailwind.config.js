// Every colour, radius and timing comes from design/design-dna.json through design/theme.cjs. Charts read the
// same file, so changing a colour there changes it everywhere.
const plugin = require('tailwindcss/plugin');
const theme = require('./projects/bank-app/src/design/theme.cjs');

/** @type {import('tailwindcss').Config} */
module.exports = {
  content: [
    "./projects/bank-app/src/**/*.{html,ts}",
  ],
  theme: {
    extend: {
      fontFamily: { sans: theme.fontFamily },
      colors: theme.colors,
      borderRadius: theme.borderRadius,
      transitionTimingFunction: { ui: 'var(--m-ease)', enter: 'var(--m-ease-enter)' },
    },
  },
  plugins: [plugin(({ addBase }) => addBase(theme.base))],
};
