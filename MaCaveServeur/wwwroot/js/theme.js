// wwwroot/js/theme.js
window.theme = (function(){
  const KEY = "ga.theme";
  function apply(t){
    document.documentElement.setAttribute("data-theme", t);
    localStorage.setItem(KEY, t);
  }
  function current(){
    return document.documentElement.getAttribute("data-theme") 
        || localStorage.getItem(KEY) 
        || (window.matchMedia("(prefers-color-scheme: dark)").matches ? "dark" : "light");
  }
  return {
    init(){
      apply(current());
    },
    toggle(){
      const t = current()==="dark" ? "light" : "dark";
      apply(t);
    }
  };
})();
