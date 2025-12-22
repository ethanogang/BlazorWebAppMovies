// Charts thème dark violet (Google Charts)
window.cellarCharts = {
  render: function (items) {
    const colors = ['#b76cff', '#7c3aed', '#c084fc', '#a78bfa', '#6d28d9'];
    google.charts.load('current', { packages: ['corechart'] });
    google.charts.setOnLoadCallback(drawAll);

    function baseOptions(title){
      return {
        title: title || '',
        backgroundColor: 'transparent',
        legend: { textStyle: { color: '#e8e8ff' } },
        titleTextStyle: { color: '#e8e8ff', bold: true },
        chartArea: { left: 50, top: 40, right: 20, bottom: 40, width: '100%', height: '70%' },
        hAxis: { textStyle: { color: '#c9c9e8' }, gridlines: { color: 'rgba(255,255,255,.06)' } },
        vAxis: { textStyle: { color: '#c9c9e8' }, gridlines: { color: 'rgba(255,255,255,.06)' } },
        colors
      };
    }

    function drawAll(){
      // --- Par région (Pie) ---
      const byRegion = {};
      items.forEach(x => byRegion[x.region || 'N/C'] = (byRegion[x.region || 'N/C'] || 0) + (x.qty || 0));
      const r = new google.visualization.DataTable();
      r.addColumn('string', 'Région'); r.addColumn('number', 'Qté');
      Object.keys(byRegion).forEach(k => r.addRow([k, byRegion[k]]));
      new google.visualization.PieChart(document.getElementById('byRegion'))
        .draw(r, Object.assign(baseOptions('Bouteilles par région'), { pieHole: .35 }));

      // --- Par millésime (Column) ---
      const byVintage = {};
      items.forEach(x => {
        const k = (x.vintage ?? '').toString() || 'N/C';
        byVintage[k] = (byVintage[k] || 0) + (x.qty || 0);
      });
      const v = new google.visualization.DataTable();
      v.addColumn('string','Millésime'); v.addColumn('number','Qté');
      Object.keys(byVintage).sort().forEach(k => v.addRow([k, byVintage[k]]));
      new google.visualization.ColumnChart(document.getElementById('byVintage'))
        .draw(v, baseOptions('Répartition par millésime'));

      // --- Valeur totale par millésime (Line) ---
      const byYearVal = {};
      items.forEach(x => {
        const k = (x.vintage ?? '').toString() || 'N/C';
        byYearVal[k] = (byYearVal[k] || 0) + (x.value || 0);
      });
      const val = new google.visualization.DataTable();
      val.addColumn('string','Année'); val.addColumn('number','Valeur (€)');
      Object.keys(byYearVal).sort().forEach(k => val.addRow([k, byYearVal[k]]));
      new google.visualization.LineChart(document.getElementById('valueOverTime'))
        .draw(val, Object.assign(baseOptions('Valeur totale par millésime'), { curveType: 'function', pointSize: 6 }));
    }
  }
};
