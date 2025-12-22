window.siteContext = {
  key: "macave.site",
  get: function () { try { return localStorage.getItem(this.key); } catch { return null; } },
  set: function (v) { try { localStorage.setItem(this.key, v || ""); } catch { } }
};
