import sys
import os
currentPath=os.path.abspath(".")
sys.path.append(currentPath)
import System
import clr
clr.AddReferenceToFile("ZeroMQ.dll")
import ZeroMQ as zmq
context = zmq.ZContext()
requester = zmq.ZSocket(context, zmq.ZSocketType.REQ)
requester.Connect("tcp://127.0.0.1:5555")

for request in range(10):
    print("Sending request %s ..." % request)
    zframe=zmq.ZFrame("Hello")
    requester.Send(zframe)
    reply = requester.ReceiveFrame()
    message=reply.ReadString()
    print("Received reply %s [ %s ]" % (request, message))