const ctx = document.getElementById('myChart').getContext('2d');
const datasetsMap = {}; // Name -> datasetIndex
const colors = [
    'rgba(75, 192, 192, 1)',
    'rgba(255, 99, 132, 1)',
    'rgba(54, 162, 235, 1)',
    'rgba(255, 206, 86, 1)',
    'rgba(153, 102, 255, 1)',
    'rgba(255, 159, 64, 1)'
];


var lineChart = new Chart(ctx, {
    type: "line",
    data: {
        labels: [],
        datasets: []
    },
    options: {
        interaction: {
            mode: 'nearest',
            axis: 'x',
            intersect: false
        },
        plugins: {
            legend: { display: true, position: 'bottom' }
        },
        scales: {
            x: {
                type: 'time',
                time: {
                    displayFormats: {
                        quarter: 'HH:mm DD.MMM.YYYY'
                    }
                }
            }
        }
    } 
});

function addData(arr) {

    arr.forEach((item) => {
        const dsIdx = ensureDataset(item.N);
        const ds = lineChart.data.datasets[dsIdx];
        ds.data.push({ x: item.T, y: item.V });
    });

    lineChart.update('none');
}

// neuen Stift erstellen, wenn Dataset nicht existiert 
function ensureDataset(name) {
    if (datasetsMap.hasOwnProperty(name)) {
        return datasetsMap[name];
    }

    const color = colors[Object.keys(datasetsMap).length % colors.length];
    const ds = {
        label: name,
        data: [], // {x: Date, y: Number} 
        borderColor: color,
        backgroundColor: color,
        pointRadius: 1,
        fill: false,
        tension: 0.0
    };

    lineChart.data.datasets.push(ds);

    const idx = lineChart.data.datasets.length - 1;
    datasetsMap[name] = idx;
    return idx;
}

// Hilfsfunktion: Datum aus Zeitstempeln 
function toDate(ts) {
    if (ts instanceof Date)
        return ts;
    const d = typeof ts === 'number' ? new Date(ts) : new Date(ts);

    return isNaN(d.getTime()) ? null : d;
}

//für später: Datensätze entfernen
function removeData(chart) {
    chart.data.labels.pop();
    chart.data.datasets.forEach((dataset) => {
        dataset.data.pop();
    });
    chart.update('none'); //ohne Animation
}
