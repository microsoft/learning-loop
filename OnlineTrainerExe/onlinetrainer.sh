#!/bin/bash
echo "Calling OnlineTrainer trainer from bash script"
dotnet Microsoft.DecisionService.OnlineTrainer.dll "$@"
exitstatus=$?
sleep 60 # sleep for some time to let the core file be created.
corefilepath="/tmp/core.${HOSTNAME}" # this path must match the contents of the file in /proc/sys/kernel/core_pattern file.

if [ -f $corefilepath ];then
        echo "core dump file found. Extracting thread info ..."
        # GDB commands:
        # sharedlibrary .
        #       Ensure that shared libary symbols are loaded.
        # bt full
        #       Print current stack trace (detailed)
        # disas
        #       Show assembly for current frame
        # info threads
        #       Print information about all threads
        # t a a bt
        #       display call stack for all threads
        gdb --batch --nx --core=$corefilepath -ex "sharedlibrary ." -ex "bt full" -ex "disas" -ex "info threads" -ex "t a a bt"
else
        echo "No core dump file found!"
fi

exit $exitstatus