module.exports = {
  content: [
    "./Pages/**/*.{cshtml,razor,html}",
    "./Shared/**/*.{razor,html}",
    "./Components/**/*.{razor,html}",
    "./**/*.razor",
    "./wwwroot/**/*.html",
    "./Styles/app.css"
  ],
  theme: {
    extend: {
      colors: {
        accent: { 
          DEFAULT: "#b76cff", 
          deep: "#7c3aed",
          light: "#dfc3ff"
        },
        wine: {
          red: "#722f37",
          burgundy: "#800020",
          rose: "#ff007f"
        },
        gold: {
          DEFAULT: "#d4af37",
          light: "#f4e4a6"
        },
        slate: {
          DEFAULT: "#334155",
          light: "#64748b",
          dark: "#1e293b"
        }
      },
      boxShadow: {
        panel: "0 18px 50px rgba(0,0,0,.5)",
        card: "0 4px 20px rgba(0,0,0,.1)",
        'card-hover': "0 8px 30px rgba(183,108,255,.3)",
        glow: "0 0 20px rgba(183,108,255,.4)"
      },
      borderRadius: {
        xl2: "16px",
        '2xl': "20px"
      },
      fontFamily: {
        'serif': ['Playfair Display', 'Georgia', 'serif'],
        'sans': ['Inter', 'system-ui', 'sans-serif']
      },
      backgroundImage: {
        'gradient-wine': 'linear-gradient(135deg, #722f37 0%, #b76cff 100%)',
        'gradient-gold': 'linear-gradient(135deg, #d4af37 0%, #f4e4a6 100%)',
        'gradient-dark': 'linear-gradient(180deg, #1e293b 0%, #0f172a 100%)'
      }
    }
  },
  plugins: []
};