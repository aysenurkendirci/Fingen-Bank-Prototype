// The saved theme, accent and mode, applied before the first paint so a dark choice never flashes light.
// ThemeService takes over once the app runs. Kept tiny and dependency-free on purpose.
(function () {
  try {
    var root = document.documentElement;
    var theme = localStorage.getItem('theme');
    var accent = localStorage.getItem('accent');
    var mode = localStorage.getItem('mode');
    if (theme === 'light' || theme === 'dark') root.setAttribute('data-theme', theme);
    if (accent && /^[a-z]+$/.test(accent)) root.setAttribute('data-accent', accent);
    if (mode === 'young') root.setAttribute('data-mode', mode);
  } catch (e) {}
})();
