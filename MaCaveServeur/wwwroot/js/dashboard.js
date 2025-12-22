// wwwroot/js/dashboard.js
window.dashboardCharts = (function () {
  function ensureGoogle(cb) {
    if (window.google && google.charts && google.visualization) {
      cb();
      return;
    }
    if (!window.google || !google.charts) {
      console.error("Google Charts loader manquant : ajoutez <script src='https://www.gstatic.com/charts/loader.js'></script> dans _Host.cshtml.");
      return;
    }
    google.charts.load('current', { packages: ['corechart', 'bar'] });
    google.charts.setOnLoadCallback(cb);
  }

  function arrayToDataTable(arr) {
    return google.visualization.arrayToDataTable(arr);
  }

  function drawPie(containerId, data, title) {
    ensureGoogle(function () {
      const el = document.getElementById(containerId);
      if (!el) return;
      const dt = arrayToDataTable(data);
      const chart = new google.visualization.PieChart(el);
      chart.draw(dt, { title, chartArea: { width: '90%', height: '80%' }, legend: { position: 'right' } });
    });
  }

  function drawBar(containerId, data, title, isStacked = false) {
    ensureGoogle(function () {
      const el = document.getElementById(containerId);
      if (!el) return;
      const dt = arrayToDataTable(data);
      const chart = new google.charts.Bar(el);
      chart.draw(dt, google.charts.Bar.convertOptions({
        title,
        isStacked,
        chartArea: { width: '85%', height: '75%' },
        legend: { position: 'none' },
        hAxis: { minValue: 0 }
      }));
    });
  }

  return {
    drawHome: function (colorsData, regionsData, sitesDataOrNull) {
      drawPie("chartColors", colorsData, "Répartition par couleur");
      drawBar("chartRegions", regionsData, "Top régions (réfs)");
      if (sitesDataOrNull) drawBar("chartSites", sitesDataOrNull, "Stock par établissement (bouteilles)");
    },
    drawDashboard: function (colorsData, regionsData, sitesDataOrNull) {
      // même rendu que drawHome, séparé pour évolutions futures
      this.drawHome(colorsData, regionsData, sitesDataOrNull);
    }
  };
})();
