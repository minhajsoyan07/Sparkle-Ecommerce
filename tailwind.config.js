/** @type {import('tailwindcss').Config} */
module.exports = {
  prefix: 'tw-',
  corePlugins: {
    preflight: false,
  },
  content: [
    "./Sparkle.Api/**/*.cshtml",
    "./Sparkle.Api/**/*.html",
    "./Sparkle.Api/**/*.js",
    "./Sparkle.Api/wwwroot/**/*.js"
  ],
  theme: {
    extend: {},
  },
  plugins: [],
}
