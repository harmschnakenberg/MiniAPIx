namespace MiniAPI
{
    public static class HtmlTemplate
    {
        public static string JSWebsocket { get; } = @"<!DOCTYPE html>
            <html>
                <head>
                    <title>WebSocket Test " + DateTime.Now.ToShortTimeString() + @"</title>
                    <script>
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
                </script>                       
                </head>
                <body>
                    <h1>WebSocket Test</h1>
                    <input type='text' id='a1' placeholder='Stunde' data-name='A01_DB10_DBW2' data-unit='std'/>                    
                    <input type='text' id='a2' placeholder='Minute' data-name='A01_DB10_DBW4' data-unit='min'/>
                    <input type='text' id='a3' placeholder='Sekunde' data-name='A01_DB10_DBW6' data-unit='s'/>
                    <input type='text' id='a4' placeholder='Wochentag' data-name='A01_DB10_DBW8'/>
                    <button onclick='sendMessage()'>Send Message</button>
                </body>
            </html>
        ";
    }
}
