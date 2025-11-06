setInterval('initTags(60);', 60000); //Auto-Refresh

let socket;
function initWebSocket() {
    socket = new WebSocket('ws://' + window.location.host + '/');
    socket.onclose = (event) => {
        console.log('WebSocket is closed now.');
        showAlert('WebSocket is closed now.');
    };
    socket.onerror = function (error) {
        console.error('WebSocket error: ' + error);
        showAlert('WebSocket error: ' + error);
    };
    socket.onopen = function (event) {
        console.log('WebSocket is open now.');
        showAlert('WebSocket is open now.');
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

function validateInput(inpObj) {
    if (!inpObj.checkValidity()) {
        alert(inpObj.validationMessage); 
    }
}

function showAlert(alert) {
    const x = document.getElementById("alerts");
    const node = document.createElement("div");
    node.className = "alert";
    const h3 = document.createElement("h3");
    const head = document.createTextNode('Meldung');    
    h3.appendChild(head);   
    node.appendChild(h3);

    const span = document.createElement("span");
    const close = document.createTextNode("x");
    span.appendChild(close);
    span.addEventListener("click", function () { this.parentElement.style.display = 'none'; }); 
    node.appendChild(span);

    const p = document.createElement("p");
    const text = document.createTextNode(alert);    
    p.appendChild(text);
    node.appendChild(p);

    x.appendChild(node);   
}

/*
if (typeof (EventSource) !== "undefined") {
    var source = new EventSource("http://" + window.location.host + "/alert");
    source.onmessage = function (event) {
        console.log("ALARM " + event.data);
        //showAlert(event.data);
    };
} else {
    x.innerHTML = "Sorry, no support for server-sent events.";
} //*/


window.onload = drawUnits;
window.onload = initWebSocket;