if (NOT DEFINED VCPKG_ROOT)
   message(FATAL_ERROR "VCPKG_ROOT is not defined. Please make sure vcpkg is installed and VCPKG_ROOT is set.")
endif()

set(RL_SIM_ARTIFACTS_PATH "${CMAKE_SOURCE_DIR}/artifacts/rl-sim")
set(VW_ARTIFACTS_PATH "${CMAKE_SOURCE_DIR}/artifacts/vw-bin")
set(RL_INSTALLED_BINARY_PATH "${VCPKG_ROOT}/installed/${VCPKG_TARGET_TRIPLET}/tools/reinforcement-learning")

if (WIN32)
   set(MKDIR_CMD "mkdir")
   set(COPY_CMD "copy")
   set(RL_SIM_SRC_EXE "rl_sim_cpp.out.exe")
   set(RL_SIM_DST_EXE "rl_sim-win-x64.exe")
   set(VW_SRC_EXE "vw.exe")
   set(VW_DST_EXE "vw-win-x64.exe")
elseif(UNIX AND NOT APPLE)
   set(MKDIR_CMD "mkdir -p")
   set(COPY_CMD "cp")
   set(RL_SIM_SRC_EXE "rl_sim_cpp.out")
   set(RL_SIM_DST_EXE "rl_sim-linux-x64")
   set(VW_SRC_EXE "vw")
   set(VW_DST_EXE "vw-linux-x64")
elseif(APPLE)
   set(MKDIR_CMD "mkdir -p")
   set(COPY_CMD "cp")
   set(RL_SIM_SRC_EXE "rl_sim_cpp.out")
   set(RL_SIM_DST_EXE "rl_sim-osx-x64")
   set(VW_SRC_EXE "vw")
   set(VW_DST_EXE "vw-osx-x64")
else()
   message(FATAL_ERROR "Unsupported platform")
endif()

execute_process(
   COMMAND ${VCPKG_ROOT}/vcpkg install reinforcement-learning[azure-auth,external-parser] --overlay-ports=${RL_PORT_PATH} --triplet=${VCPKG_TARGET_TRIPLET}
)

if (NOT EXISTS "${RL_INSTALLED_BINARY_PATH}")
   message(FATAL_ERROR "reinforcement learning binaries not found at ${RL_INSTALLED_BINARY_PATH}")
endif()

file(MAKE_DIRECTORY ${RL_SIM_ARTIFACTS_PATH})
file(MAKE_DIRECTORY ${VW_ARTIFACTS_PATH})
file(COPY_FILE "${RL_INSTALLED_BINARY_PATH}/${RL_SIM_SRC_EXE}" "${RL_SIM_ARTIFACTS_PATH}/${RL_SIM_DST_EXE}" ONLY_IF_DIFFERENT)
file(COPY_FILE "${RL_INSTALLED_BINARY_PATH}/${VW_SRC_EXE}" "${VW_ARTIFACTS_PATH}/${VW_DST_EXE}" ONLY_IF_DIFFERENT)
