// Turns design-dna.json into what Tailwind needs: colour names that point at CSS variables, and the variables
// themselves for every theme and accent. Components only ever say "bg-brand"; which hex that is depends on the
// theme (light or dark) and on the accent the customer picked.
const dna = require('./design-dna.json');

const { color, accents, shape, motion, typography } = dna.design_system;
const [defaultAccent] = Object.keys(accents);

const rgb = (hex) => {
  const n = parseInt(hex.slice(1), 16);
  return `${(n >> 16) & 255} ${(n >> 8) & 255} ${n & 255}`;
};

// Neutrals and status colours of a theme, with the accent's colours laid over them.
const merge = (base, over) => {
  const out = { ...base };
  for (const [key, value] of Object.entries(over)) {
    out[key] = typeof value === 'object' && typeof base[key] === 'object' ? merge(base[key], value) : value;
  }
  return out;
};

const palette = (theme, accent) => merge(color[theme], { brand: accents[accent][theme].brand, tile: accents[accent][theme].tile });

// { ink: { DEFAULT, soft } } -> [['ink', '#..'], ['ink-soft', '#..']]
const flatten = (tokens, prefix = '') =>
  Object.entries(tokens).flatMap(([key, value]) => {
    const name = key === 'DEFAULT' ? prefix : prefix ? `${prefix}-${key}` : key;
    return typeof value === 'string' ? [[name, value]] : flatten(value, name);
  });

const variables = (tokens) => Object.fromEntries(flatten(tokens).map(([name, hex]) => [`--c-${name}`, rgb(hex)]));

// The colours an accent changes, for one theme.
const accentVariables = (accent, theme) =>
  variables({ brand: accents[accent][theme].brand, tile: accents[accent][theme].tile });

// Same shape as the palette, but every leaf reads its variable, keeping Tailwind's opacity modifiers working.
const colors = (tokens, prefix = '') =>
  Object.fromEntries(
    Object.entries(tokens).map(([key, value]) => {
      const name = key === 'DEFAULT' ? prefix : prefix ? `${prefix}-${key}` : key;
      return [key, typeof value === 'string' ? `rgb(var(--c-${name}) / <alpha-value>)` : colors(value, name)];
    }),
  );

const shared = {
  ...Object.fromEntries(Object.entries(motion).map(([key, value]) => [`--m-${key}`, value])),
  ...Object.fromEntries(Object.entries(shape.radius).map(([key, value]) => [`--r-${key}`, value])),
};

// Selectors grow more specific in the order they must win: the theme, then an accent, then both together.
const accentRules = Object.keys(accents)
  .filter((accent) => accent !== defaultAccent)
  .reduce(
    (rules, accent) => {
      const on = `:root[data-accent="${accent}"]`;
      rules[on] = accentVariables(accent, 'light');
      rules['@media (prefers-color-scheme: dark)'][`${on}:not([data-theme="light"])`] = accentVariables(accent, 'dark');
      rules[`${on}[data-theme="dark"]`] = accentVariables(accent, 'dark');
      return rules;
    },
    { '@media (prefers-color-scheme: dark)': {} },
  );

module.exports = {
  colors: colors(palette('light', defaultAccent)),
  fontFamily: [typography.family, ...typography.fallback.split(',').map((f) => f.trim())],
  borderRadius: Object.fromEntries(Object.keys(shape.radius).map((key) => [key, `var(--r-${key})`])),

  /** CSS for the base layer: light by default, dark when the system asks for it or the user picked it. */
  base: [
    {
      ':root': { ...shared, ...variables(palette('light', defaultAccent)), colorScheme: 'light' },
      '@media (prefers-color-scheme: dark)': {
        ':root:not([data-theme="light"])': { ...variables(palette('dark', defaultAccent)), colorScheme: 'dark' },
      },
      ':root[data-theme="dark"]': { ...variables(palette('dark', defaultAccent)), colorScheme: 'dark' },
    },
    accentRules,
  ],
};
