# copied and modified from reinforcement_learning/CMakeLists.txt
cmake_minimum_required(VERSION 3.10)
if(POLICY CMP0074)
    cmake_policy(SET CMP0074 NEW)
endif()
if(POLICY CMP0091)
    cmake_policy(SET CMP0091 NEW)
endif()

if(WIN32)
  # Due to needing to configure the CMAKE platform, this needs to be included before the
  # top-level project() declaration.
  include(${CMAKE_CURRENT_SOURCE_DIR}/reinforcement_learning/cmake/platforms/win32.cmake)
  message(STATUS "WinSDK Version: ${CMAKE_SYSTEM_VERSION}")
endif()

if(NOT WIN32)
  set(CMAKE_CXX_FLAGS_RELWITHDEBINFO "-O3 -g -DNDEBUG")
  set(CMAKE_C_FLAGS_RELWITHDEBINFO "-O3 -g -DNDEBUG")
endif()

if (NOT CMAKE_BUILD_TYPE AND NOT GENERATOR_IS_MULTI_CONFIG)
  message(STATUS "No build type selected, defaulting to Release")
  set(CMAKE_BUILD_TYPE "Release")
endif()

if(RL_LINK_AZURE_LIBS)
  list(APPEND VCPKG_MANIFEST_FEATURES "azurelibs")
endif()

project(rl_component_build)

# Add support for building library with latest version of C++ supported by your compiler
# Copied from VW
set(CMAKE_MODULE_PATH ${CMAKE_MODULE_PATH} "${CMAKE_CURRENT_LIST_DIR}/reinforcement_learning/cmake/")

set(RL_CXX_STANDARD 14)
message(STATUS "Using C++ standard: " ${RL_CXX_STANDARD})
set(CMAKE_CXX_STANDARD ${RL_CXX_STANDARD})
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_CXX_EXTENSIONS OFF)

if(UNIX AND NOT APPLE)
  # Temporary workaround for VW 9.6 missing size_t on Linux builds
  # TODO remove when VW submodule is updated
  # This must be stddef.h and not cstddef because it also applies to C code
  add_compile_options(-include stddef.h)
  # Enable C++17 math functions in C++11
  add_compile_definitions(__STDCPP_WANT_MATH_SPEC_FUNCS__)
endif()

if(RL_STATIC_DEPS)
  if(WIN32)
    set(STATIC_LIB_SUFFIXES ".lib" ".a")
  else()
    set(STATIC_LIB_SUFFIXES ".a")
  endif()

  set(BUILD_SHARED_LIBS OFF CACHE BOOL "")
  set(STATIC_LINK_VW_JAVA ON CACHE BOOL "")
  set(Boost_USE_STATIC_LIBS ON CACHE BOOL "")
  set(CMAKE_FIND_LIBRARY_SUFFIXES ${STATIC_LIB_SUFFIXES})
endif()

# We also require Boost::uuid, but that is a header-only dependency and is unable to be found
# using find_package as a component, which means it is possible to have a successfully configured
# build that will nonetheless not work due to missing dependencies.
find_package(Boost COMPONENTS unit_test_framework system program_options thread REQUIRED)

# On MacOS, Boost::thread needs to be linked separately.
if (${CMAKE_SYSTEM_NAME} MATCHES "Darwin")
  find_package(Boost COMPONENTS thread REQUIRED)
endif()

include(ProcessorCount)
ProcessorCount(NumProcessors)
message("Number of processors: ${NumProcessors}")
file(WRITE ${CMAKE_CURRENT_BINARY_DIR}/nprocs.txt ${NumProcessors})

# This provides the variables such as CMAKE_INSTALL_LIBDIR for installation paths.
include(GNUInstallDirs)

include(reinforcement_learning/ext_libs/ext_libs.cmake)

add_subdirectory(reinforcement_learning/external_parser)
add_subdirectory(reinforcement_learning/rlclientlib)
add_subdirectory(reinforcement_learning/examples/rl_sim_cpp)
