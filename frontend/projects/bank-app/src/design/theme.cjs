// Turns design-dna.json into what Tailwind needs: colour names that point at CSS variables, and the variables
// themselves for each theme. Components only ever say "bg-surface"; which hex that is depends on the theme.
const dna = require('./design-dna.json');

const { color, shape, motion, typography } = dna.design_system;

const rgb = (hex) => {
  const n = parseInt(hex.slice(1), 16);
  return `${(n >> 16) & 255} ${(n >> 8) & 255} ${n & 255}`;
};

// { ink: { DEFAULT, soft } } -> [['ink', '#..'], ['ink-soft', '#..']]
const flatten = (tokens, prefix = '') =>
  Object.entries(tokens).flatMap(([key, value]) => {
    const name = key === 'DEFAULT' ? prefix : prefix ? `${prefix}-${key}` : key;
    return typeof value === 'string' ? [[name, value]] : flatten(value, name);
  });

const variables = (palette) =>
  Object.fromEntries(flatten(palette).map(([name, hex]) => [`--c-${name}`, rgb(hex)]));

// Same shape as the palette, but every leaf reads its variable, keeping Tailwind's opacity modifiers working.
const colors = (palette, prefix = '') =>
  Object.fromEntries(
    Object.entries(palette).map(([key, value]) => {
      const name = key === 'DEFAULT' ? prefix : prefix ? `${prefix}-${key}` : key;
      return [key, typeof value === 'string' ? `rgb(var(--c-${name}) / <alpha-value>)` : colors(value, name)];
    }),
  );

const shared = {
  ...Object.fromEntries(Object.entries(motion).map(([key, value]) => [`--m-${key}`, value])),
  ...Object.fromEntries(Object.entries(shape.radius).map(([key, value]) => [`--r-${key}`, value])),
};

module.exports = {
  colors: colors(color.light),
  fontFamily: [typography.family, ...typography.fallback.split(',').map((f) => f.trim())],
  borderRadius: Object.fromEntries(Object.keys(shape.radius).map((key) => [key, `var(--r-${key})`])),

  /** CSS for the base layer: light by default, dark when the system asks for it or the user picked it. */
  base: {
    ':root': { ...shared, ...variables(color.light), colorScheme: 'light' },
    '@media (prefers-color-scheme: dark)': {
      ':root:not([data-theme="light"])': { ...variables(color.dark), colorScheme: 'dark' },
    },
    ':root[data-theme="dark"]': { ...variables(color.dark), colorScheme: 'dark' },
  },
};
