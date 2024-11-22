# builds the learning-loop project and its dependencies
# use presets to build multiple configuration builds for ninja or visual studio
#
# DotNet and a C++ compiler are required to build the project.  C++ is needed to
# the reinforcement learning binaries; the build is provided as VCPKG port located
# in the port directory at the root of learning-loop.
#
# use the following command to build the project:
#
#    replace <your-vcpkg-root> with the path to your vcpkg root
#
#    Ninja:
#     cmake --preset=ninja -DVCPKG_ROOT=<your-vcpkg-root>
#     cmake --build --preset=release
#
#    Visual Studio:
#     cmake --preset=vs2017 -DVCPKG_ROOT=<your-vcpkg-root>
#     cmake --build --preset=release
#
# Available targets:
#
#    learning-loop: builds projects in the learning-loop solution
#    onlinetrainer: builds the OnlineTrainerExe project and dependencies
#    common: builds the Common project and dependencies
#    nuget: builds the Common project and creates a nuget package
#
# Override LL_VERSION and LL_INFO_VERSION to set the version of the learning-loop project
# eg: cmake --build --preset=release -DLL_VERSION=2.0.1.2 -DLL_INFO_VERSION=2.0.1.2
cmake_minimum_required(VERSION 3.5)
set(DIRECTORY PROPERTY DEFAULT_TARGET learning-loop)

# setup the triplet for the reinforcement-learning vcpkg port
if(WIN32)
   set(VCPKG_TARGET_TRIPLET "x64-windows-static")
elseif(UNIX AND NOT APPLE)
   set(VCPKG_TARGET_TRIPLET "x64-linux")
elseif(APPLE)
   set(VCPKG_TARGET_TRIPLET "x64-osx")
else()
   message(FATAL_ERROR "Unsupported platform") 
endif()

# default the learning-loop version info
set(LL_VERSION "1.0.0.0")
set(LL_INFO_VERSION "1.0.0.0-dev")

# include the reinforcement-learning port build
include(./rl-binaries.cmake)

# setup the learning-loop project and targets
project(learning-loop)

add_custom_target(rl-binaries)

add_custom_target(learning-loop ALL
   COMMAND dotnet build ${CMAKE_CURRENT_SOURCE_DIR}/learning-loop.sln -c $<CONFIG> -p:CIVWBinParserPath="${CMAKE_CURRENT_SOURCE_DIR}/artifacts/vw-bin" -p:PackagingIntegration=${CMAKE_CURRENT_SOURCE_DIR}/.build/microsoft/DelaySign.props -p:AssemblyVersion=${LL_VERSION} -p:FileVersion=${LL_VERSION} -p:InformationalVersion=${LL_INFO_VERSION}
   DEPENDS rl-binaries
)

add_custom_target(onlinetrainer ALL
   COMMAND dotnet build ${CMAKE_CURRENT_SOURCE_DIR}/OnlineTrainerExe/OnlineTrainerExe.csproj -c $<CONFIG> -p:CIVWBinParserPath="${CMAKE_CURRENT_SOURCE_DIR}/artifacts/vw-bin" -p:PackagingIntegration=${CMAKE_CURRENT_SOURCE_DIR}/.build/microsoft/DelaySign.props -p:AssemblyVersion=${LL_VERSION} -p:FileVersion=${LL_VERSION} -p:InformationalVersion=${LL_INFO_VERSION}
   COMMAND dotnet publish ${CMAKE_CURRENT_SOURCE_DIR}/OnlineTrainerExe/OnlineTrainerExe.csproj --no-build -c $<CONFIG> -o "${CMAKE_CURRENT_SOURCE_DIR}/artifacts/OnlineTrainer"
   DEPENDS rl-binaries
)

add_custom_target(common ALL
   COMMAND dotnet build ${CMAKE_CURRENT_SOURCE_DIR}/Common/Common.csproj -c $<CONFIG> -p:CIVWBinParserPath="${CMAKE_CURRENT_SOURCE_DIR}/artifacts/vw-bin" -p:PackagingIntegration=${CMAKE_CURRENT_SOURCE_DIR}/.build/microsoft/DelaySign.props -p:AssemblyVersion=${LL_VERSION} -p:FileVersion=${LL_VERSION} -p:InformationalVersion=${LL_INFO_VERSION}
   COMMAND dotnet publish ${CMAKE_CURRENT_SOURCE_DIR}/Common/Common.csproj --no-build -c $<CONFIG> -o "${CMAKE_CURRENT_SOURCE_DIR}/artifacts/Common"
   DEPENDS rl-binaries
)

if (WIN32)
   # for windows, nuget pack needs the path to be backslashes in order for wildcard specs to work
   string(REPLACE "/" "\\" PACKAGE_SOURCE_PATH_WIN ${CMAKE_CURRENT_SOURCE_DIR})
   add_custom_target(nuget ALL
      COMMAND nuget pack ${CMAKE_CURRENT_SOURCE_DIR}/Common/Microsoft.DecisionService.Common.nuspec  -Properties "PackageSourcePath=${PACKAGE_SOURCE_PATH_WIN}\\artifacts\\Common;version=${LL_VERSION};targetframework=net8.0" -OutputDirectory ${CMAKE_CURRENT_SOURCE_DIR}/artifacts/nuget
      DEPENDS common
   )
else()
   add_custom_target(nuget ALL
      COMMAND dotnet pack ${CMAKE_CURRENT_SOURCE_DIR}/Common/Common.csproj --no-build -c $<CONFIG> --no-build -p:PackageSourcePath="${CMAKE_CURRENT_SOURCE_DIR}/artifacts/Common" -p:PackagingIntegration=${CMAKE_CURRENT_SOURCE_DIR}/.build/microsoft/DelaySign.props -o "${CMAKE_CURRENT_SOURCE_DIR}/artifacts/nuget" -p:PackageVersion=${LL_VERSION}
      DEPENDS common
   )
endif()

add_custom_target(console-joiner ALL
   COMMAND dotnet build ${CMAKE_CURRENT_SOURCE_DIR}/examples/ConsoleJoiner/ConsoleJoiner.csproj -c $<CONFIG> -p:PackagingIntegration=${CMAKE_CURRENT_SOURCE_DIR}/.build/microsoft/DelaySign.props -p:AssemblyVersion=${LL_VERSION} -p:FileVersion=${LL_VERSION} -p:InformationalVersion=${LL_INFO_VERSION}
   COMMAND dotnet publish ${CMAKE_CURRENT_SOURCE_DIR}/examples/ConsoleJoiner/ConsoleJoiner.csproj --no-build -c $<CONFIG> -o "${CMAKE_CURRENT_SOURCE_DIR}/artifacts/examples/ConsoleJoiner"
   DEPENDS nuget
)

add_custom_target(examples ALL
   DEPENDS console-joiner
)

if (WIN32)
   string(REPLACE "/" "\\" WIN_CURRENT_SOURCE_PATH ${CMAKE_CURRENT_SOURCE_DIR})
   add_custom_target(docker ALL
      COMMAND docker build -t learning-loop -f "${WIN_CURRENT_SOURCE_PATH}\\..\\docker\\windows-core.dockerfile" "${WIN_CURRENT_SOURCE_PATH}"
      DEPENDS onlinetrainer
   )
   add_custom_target(build-deploy ALL
      COMMAND ${CMAKE_COMMAND} -E make_directory "${WIN_CURRENT_SOURCE_PATH}\\artifacts\\docker"
      COMMAND docker save learning-loop:latest -o "${WIN_CURRENT_SOURCE_PATH}\\artifacts\\docker\\learning-loop-windows-latest.tar"
      DEPENDS docker
   )
   message(WARNING "Docker build is supported; BUT, deploy is not supported on Windows at this time")
   # TODO: modify deployment script to support Windows containers
else()
   add_custom_target(docker ALL
      COMMAND docker build -t learning-loop -f ${CMAKE_CURRENT_SOURCE_DIR}/../docker/ubuntu.dockerfile ${CMAKE_CURRENT_SOURCE_DIR}
      DEPENDS onlinetrainer
   )
   add_custom_target(build-deploy ALL
      COMMAND mkdir -p ${CMAKE_CURRENT_SOURCE_DIR}/artifacts/docker
      COMMAND docker save learning-loop:latest -o ${CMAKE_CURRENT_SOURCE_DIR}/artifacts/docker/learning-loop-ubuntu-latest.tar
      COMMAND cd ${CMAKE_CURRENT_SOURCE_DIR}/../deploy
      COMMAND chmod +x ./scripts/deploy-sample.sh
      COMMAND ./scripts/deploy-sample.sh --dockerImageFile ../src/artifacts/docker/learning-loop-ubuntu-latest.tar
      COMMAND cd ${CMAKE_CURRENT_SOURCE_DIR}
      DEPENDS docker
   )
endif()