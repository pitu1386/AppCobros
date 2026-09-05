/** @type {import('tailwindcss').Config} */
// Los colores son tokens semánticos definidos como variables CSS en wwwroot/css/app.css.
// Cambian con el tema (clase `dark` en <html>) sin tocar los componentes.
const t = (name) => `rgb(var(--c-${name}) / <alpha-value>)`;

module.exports = {
  darkMode: 'class',
  content: [
    './wwwroot/index.html',
    './App.razor',
    './Components/**/*.razor',
    './Layout/**/*.razor',
    './Pages/**/*.razor',
    './Services/**/*.cs',
  ],
  theme: {
    extend: {
      fontFamily: {
        sans: ['Barlow', 'Helvetica Neue', 'Helvetica', 'Arial', 'sans-serif'],
        cond: ['"Barlow Condensed"', '"Arial Narrow"', 'Helvetica Neue', 'Arial', 'sans-serif'],
      },
      colors: {
        app: t('app'),
        surface: t('surface'),
        well: t('well'),
        well2: t('well2'),
        line: t('line'),
        line2: t('line2'),
        ink: t('ink'),
        ink2: t('ink2'),
        muted: t('muted'),
        faint: t('faint'),
        brand: t('brand'),
        branddeep: t('branddeep'),
        brandtext: t('brandtext'),
        brandbg: t('brandbg'),
        ok: t('ok'),
        oktext: t('oktext'),
        okbg: t('okbg'),
        warn: t('warn'),
        warntext: t('warntext'),
        warnbg: t('warnbg'),
        danger: t('danger'),
        dangertext: t('dangertext'),
        dangerbg: t('dangerbg'),
        info: t('info'),
        infotext: t('infotext'),
        infobg: t('infobg'),
        hero: t('hero'),
        herotext: t('herotext'),
        heromuted: t('heromuted'),
        heroline: t('heroline'),
      },
      zIndex: { 60: '60', 70: '70' },
    },
  },
  plugins: [],
};
