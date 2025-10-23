namespace MiniAPI
{
    public static class HtmlTemplate
    {
        public static string JSWebsocket { get; } = @"<!DOCTYPE html>
            <html>
                <head>
                    <title>WebSocket Test</title>
                    <link rel='shortcut icon' href='https://www.kreutztraeger-kaeltetechnik.de/wp-content/uploads/2016/12/favicon.ico'>
                    <script src='/js/script.js'></script>   
                    <link rel='stylesheet' href='https://www.w3schools.com/w3css/5/w3.css'> 
                    <style>
                        input[data-name] {
                        width:100px;
                        text-align: right;
                        }
                    </style
                </head>
                <body>
                    <h1>WebSocket Test " + DateTime.Now + @"</h1>
                    <div class='w3-flex' style='gap:8px'>
                        <div class='w3-padding w3-green'>
                            <label>Stunde</label>
                            <input class='w3-input' type='text' data-name='A01_DB10_DBW2' data-unit='std'>
                        </div>
                        <div class='w3-padding w3-green'>
                            <label>Minute</label>
                            <input class='w3-input' type='text' data-name='A01_DB10_DBW4' data-unit='min'>
                        </div>
                        <div class='w3-padding w3-green'>
                            <label>Sekunde</label>
                            <input class='w3-input' type='text' data-name='A01_DB10_DBW6' data-unit='sec'>
                        </div>
                    </div> 
                                     
                    <input type='text' id='a2' placeholder='Minute' data-name='A01_DB10_DBW4' data-unit='min'/>
                    <input type='text' id='a3' placeholder='Sekunde' data-name='A01_DB10_DBW6' data-unit='s'/>
                    <input type='text' id='a4' placeholder='Wochentag' data-name='A01_DB10_DBW8'/>
                    <button onclick='sendMessage()'>Send Message</button>
                </body>
            </html>
        ";
    }
}

/* FUNKTIONAL!

setInterval('location.reload(true);', 90000); //Auto-Refresh
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
                                const tags = [];
                                const myCollection = document.getElementsByTagName('input');

                                for (let i = 0; i < myCollection.length; i++) {
                                    if (myCollection[i].hasAttribute('data-name')) {
                                        const tagName = myCollection[i].getAttribute('data-name');
                                        tags.push(tagName);
                                    }
                                }
                                const message = JSON.stringify(tags);
                                socket.send(message);

                            };
                            socket.onmessage = function (event) {
                                console.log(event.data);
                                let arr = JSON.parse(event.data);
                               
                                const myCollection = document.getElementsByTagName('input');

                                for (let i = 0; i < myCollection.length; i++) {
                                    if (myCollection[i].hasAttribute('data-name')) {
                                        const tagName = myCollection[i].getAttribute('data-name');
                                        let obj = arr.find(o => o.N === tagName);
                                        if (obj) {
                                            let unit = myCollection[i].getAttribute('data-unit');
                                            myCollection[i].value = obj.V + (unit ? ' ' + unit : '');
                                        }
                                    }
                                }
                            }
                        };

                        function sendMessage() {
                            const message = document.getElementById('messageInput').value;
                            socket.send(message);
                        }
                        window.onload = initWebSocket;

//*/