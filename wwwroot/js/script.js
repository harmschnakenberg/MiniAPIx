setInterval('initTags();', 60000); //Auto-Refresh

let socket;
function initWebSocket() {
    socket = new WebSocket('ws://' + window.location.host + '/');
    socket.onclose = (event) => {
        console.log('WebSocket is closed now.');
    };
    socket.onerror = function (error) {
        console.error('WebSocket error: ' + error);
    };
    socket.onopen = function (event) {
        console.log('WebSocket is open now.');

        const myCollection = document.getElementsByTagName('input');

        for (let i = 0; i < myCollection.length; i++) {
            if (myCollection[i].hasAttribute('data-unit')) {
                const tagUnit = myCollection[i].getAttribute('data-unit');
                const para = document.createElement("span");
                const node = document.createTextNode(tagUnit);
                para.appendChild(node);

                myCollection[i].parentNode.insertBefore(para, myCollection[i].nextSibling);
            }
        }
            initTags();
    };
    socket.onmessage = function (event) {
        console.log(event.data);
        let arr = JSON.parse(event.data);        
        addData(arr); //Test

        let alert = arr.find(o => o.N === "alert");
        if (alert != null) {
            showAlert(alert.V, alter.T)
        }

        const myCollection = document.getElementsByTagName('input');
        for (let i = 0; i < myCollection.length; i++) {
            if (myCollection[i].hasAttribute('data-name')) {
                const tagName = myCollection[i].getAttribute('data-name');
                let obj = arr.find(o => o.N === tagName);
                if (obj) {
                    myCollection[i].value = obj.V;
                }
            }
        }
    }
};

function sendMessage() {
    const message = document.getElementById('messageInput').value;
    socket.send(message);
}

function drawUnits() {
    const myCollection = document.getElementsByTagName('input');

    for (let i = 0; i < myCollection.length; i++) {
        if (myCollection[i].hasAttribute('data-unit')) {
            const tagUnit = myCollection[i].getAttribute('data-unit');
            const para = document.createElement("span");
            const node = document.createTextNode(tagUnit);
            para.appendChild(node);
            
            myCollection[i].parentNode.appendChild(para, myCollection[i]); 
        }
    }
}

function initTags() {
    const tags = [];
    const myCollection = document.getElementsByTagName('input');

    for (let i = 0; i < myCollection.length; i++) {

        if (myCollection[i].hasAttribute('data-name')) {
            const tagName = myCollection[i].getAttribute('data-name');
            if (!tags.includes(tagName)) {
                tags.push(tagName);
            }
        }
    }

    const message = JSON.stringify(tags);
    socket.send(message);
    document.getElementById('loadtime').innerHTML = new Date().toLocaleTimeString();
}

function showAlert(alert, time) {
    const para = document.createElement("div");
    const node = document.createTextNode(alert + '\r\n' + time);
    para.appendChild(node);
    para.style.backgroundColor = 'red';
    document.body.appendChild(para);
}

function validateInput(inpObj) {
    if (!inpObj.checkValidity()) {
        alert(inpObj.validationMessage); 
    }
}


window.onload = drawUnits;
window.onload = initWebSocket;