start ..\source\CableCloud\CableCloud\bin\Debug\netcoreapp3.0\CableCloud.exe 
timeout 1
start ..\source\CCRC2\CCRC2\bin\Debug\netcoreapp3.0\CCRC2.exe "CCRC2.txt"
timeout 1
start ..\source\CCRC3\CCRC3\bin\Debug\netcoreapp3.0\CCRC3.exe "CCRC3.txt"
timeout 1
start ..\source\Controllers\Controllers\bin\Debug\netcoreapp3.0\Controllers.exe "CCRC1.txt"
timeout 1
start ..\source\NCC\NCC\NCC\bin\Debug\netcoreapp3.0\NCC.exe "NCC1config.txt"
timeout 1
start ..\source\NCC\NCC\NCC\bin\Debug\netcoreapp3.0\NCC.exe "NCC2config.txt"
timeout 1
start ..\source\Host\Host\bin\Debug\Host.exe "h1_config.txt"
timeout 1
start ..\source\Host\Host\bin\Debug\Host.exe "h2_config.txt"
timeout 1
start ..\source\Host\Host\bin\Debug\Host.exe "h3_config.txt"
timeout 1
start ..\source\OpticalNode\OpticalNode\bin\Debug\netcoreapp3.0\OpticalNode.exe "node1_config.txt"
timeout 1
start ..\source\OpticalNode\OpticalNode\bin\Debug\netcoreapp3.0\OpticalNode.exe "node2_config.txt"
timeout 1
start ..\source\OpticalNode\OpticalNode\bin\Debug\netcoreapp3.0\OpticalNode.exe "node3_config.txt"
timeout 1
start ..\source\OpticalNode\OpticalNode\bin\Debug\netcoreapp3.0\OpticalNode.exe "node4_config.txt"
timeout 1
start ..\source\OpticalNode\OpticalNode\bin\Debug\netcoreapp3.0\OpticalNode.exe "node5_config.txt"
timeout 1
start ..\source\OpticalNode\OpticalNode\bin\Debug\netcoreapp3.0\OpticalNode.exe "node6_config.txt"
timeout 1
start ..\source\OpticalNode\OpticalNode\bin\Debug\netcoreapp3.0\OpticalNode.exe "node7_config.txt"
timeout 1
start ../cmdow.exe N1 /mov 0 0
start ../cmdow.exe N2 /mov 0 170
start ../cmdow.exe N3 /mov 0 340
start ../cmdow.exe N4 /mov 0 510
start ../cmdow.exe N5 /mov 320 0
start ../cmdow.exe N6 /mov 320 170
start ../cmdow.exe N7 /mov 320 340
start ../cmdow.exe H1 /mov 640 0
start ../cmdow.exe H2 /mov 640 500
start ../cmdow.exe H3 /mov 640 250
start ../cmdow.exe CableCloud /mov 320 500
start ../cmdow.exe NCC1 /mov 1100 0 /act
start ../cmdow.exe NCC2 /mov 1100 170 /act
start ../cmdow.exe CCRC1 /mov 1100 340 /act
start ../cmdow.exe CCRC2 /mov 1100 510 /act
start ../cmdow.exe CCRC3 /mov 1100 680 /act