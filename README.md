# Network Emulation

This project has been made to emulate a network in EON technology. It is based on the ASON model.

## Topology

![](https://github.com/Saiter711/Network_Emulation/blob/master/extra/topology.PNG)

## The looks of the project

This project is based on console applications, which communicate with each other. You can see how it looks like on a picture below:

![](https://github.com/Saiter711/Network_Emulation/blob/master/extra/snip1.PNG)

## How it works

The basic problem set in this project is for programmes to communicate with each other in order to deliver a message from one host to another.
To complicate things, the communication must be (and is) based on ASON model (G. 8080).

Setting a connection with another host (in the same domain) and sending its a message:
 
 ![](https://github.com/Saiter711/Network_Emulation/blob/master/extra/message_sent1.PNG)
 
 First we need to fill all the information in the host that we are sending a message from (a message itself, capacity of a connection to which host do you want to send it to).
 Then, the host on the other end of the line has to accept the call. If it does, the network sets up a connection and reserves needed resources (based on capacity).
 
 We can also send message to host in another domain:
 
 ![](https://github.com/Saiter711/Network_Emulation/blob/master/extra/message_sent2.PNG)
 
 ### Closing a connection
 
 If we don't want to have a connection with another host anymore, it is possible to do that too. You just have to close it in the host window, just as shown below: 
 
 ![](https://github.com/Saiter711/Network_Emulation/blob/master/extra/closing_connection1.PNG)
 
 If there the host would like to send a message to the host which it's closed the connection with again, it would have to set a connection once again.
 
 ### Finding a way
 
 If the host wants a connection to be set up, the contoller has to find a path, which has the free resources available. 
 The thing we used is an "improved" Dijkstra algorithm - finding the shortest path with available resources that we need.
 
 ### Reconnect!
 
 In our project it is possible to mock something bad happening with a link (shut down of router's interface).
 The controller sees, that something happened and for every connection which used a broken link, it finds a new way so that the hosts never find out something wrong happened in a network.
 
 There's an example below:
 
 ![](https://github.com/Saiter711/Network_Emulation/blob/master/extra/reactivating_connection1.PNG)
 
 There is a message sent to host again.
 
 ## There are always ways to improve
 
We are aware of the fact that this project isn't perfect and has a bunch of flaws.
* There are errors somewhere that would have to be fixed (such as closing a connection multiple times can lead to a situation where you can't send a message anymore).
* There are also certain logs on the consoles that we didn't have time to write.
* And many more :)
However, there isn't anything that is perfect and couldn't be better.

## Other authors of the project:

* K. Czapliñska
* K. Czerkas
* W. Kondrusik
 
 
 