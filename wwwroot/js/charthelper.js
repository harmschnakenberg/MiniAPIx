
/*
    // Canvas und Chart-Instanz vorbereiten 
//const ctx = document.getElementById('myChart2').getContext('2d');
const datasetsMap = {}; // Name -> datasetIndex 
const colors = ['rgba(75, 192, 192, 1)', 'rgba(255, 99, 132, 1)', 'rgba(54, 162, 235, 1)', 'rgba(255, 206, 86, 1)', 'rgba(153, 102, 255, 1)', 'rgba(255, 159, 64, 1)'];

// Chart initialisieren 
var chart2 = new Chart("myChart2", {
    type: 'line',
    data: {
        labels: [],
        // X-Achse: Zeitstempel (optional, wir setzen Punkte manuell) 
        datasets: []
    },
    options: {
        responsive: true,
        interaction: {
            mode: 'nearest',
            axis: 'x',
            intersect: false
        },
        scales: {
            x:
            {
                title: { display: true, text: 'Zeit' },
                type: 'time',
                time: {
                    tooltipFormat: 'YYYY-MM-DD HH:mm:ss',
                    displayFormats: { millisecond: 'HH:mm:ss.SSS', second: 'HH:mm:ss', minute: 'HH:mm', hour: 'HH:mm' }
                }               
            },
            y: {
                title: { display: true, text: 'Wert' },
                type: 'linear',
                min: 0,
                max: 100
            }
        },
        plugins: {
            legend: { display: true, position: 'bottom' }
        },
        parsing: false

        // manuelles {x, y}-Punkte verwenden 
    }
});

    // Hilfsfunktion: Datum aus Zeitstempeln 
function toDate(ts) {
    if (ts instanceof Date)
        return ts;
    const d = typeof ts === 'number' ? new Date(ts) : new Date(ts);

    return isNaN(d.getTime()) ? null : d;
}

    // Ensure ein Dataset existiert 
function ensureDataset(name) {
    if (datasetsMap.hasOwnProperty(name)) {
        return datasetsMap[name];
    }

    const color = colors[Object.keys(datasetsMap).length % colors.length];
    const ds = {
        label: name, data: [], // {x: Date, y: Number} 
        borderColor: color, backgroundColor: color, fill: false, tension: 0.4
    };

    chart.data.datasets.push(ds);

    const idx = chart.data.datasets.length - 1;
    datasetsMap[name] = idx;
    return idx;
}

    // Record verarbeiten: { Name, Zeitstempel, Wert } 
function processRecord(arr) {
    for (const record of arr) {

        if (!record || !record.N)
            return;

        const name = record.N;
        const date = toDate(record.T != null ? record.T : new Date());
        const value = record.V !== undefined ? record.V : record.Value;

        if (value === undefined || value === null)
            return;

        const dsIdx = ensureDataset(name);
        const ds = chart2.data.datasets[dsIdx];
        ds.data.push({ x: date, y: value });

        // Optional: Labels aktualisieren (X-Achse wird über Datenpunkte bestimmt)
        // chart.data.labels = []; // Falls du explizite Labels willst, hier füllen
    }
    chart2.update('none');
}

//*/