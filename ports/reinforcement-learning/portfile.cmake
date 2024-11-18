vcpkg_check_linkage(ONLY_STATIC_LIBRARY)

vcpkg_from_github(
    OUT_SOURCE_PATH SOURCE_PATH
    REPO VowpalWabbit/reinforcement_learning
    REF 84d54903df53dde642606118c83009961cfdeb6e
    SHA512 e049e2afd82692b00d9253c225071af13fb1df036af0b326b989f4169755aea94daed484fc1f6b300d00c0a0ae39e987630f387e82c7c9ad060afdea541be821
    HEAD_REF master
)

##########################################################################################################################################
## get reinforcement_learning dependencies

# cpprestsdk submodule
vcpkg_from_github(
    OUT_SOURCE_PATH CCPRESTSDK_SOURCE_PATH
    REPO microsoft/cpprestsdk
    REF 122d09549201da5383321d870bed45ecb9e168c5
    SHA512 c9ded33d3c67880e2471e479a38b40a14a9ff45d241e928b6339eca697b06ad621846260eca47b6b1b8a2bc9ab7bf4fea8d3e8e795cd430d8839beb530e16dd7
    HEAD_REF master
)

# openssl submodule
vcpkg_from_github(
    OUT_SOURCE_PATH OPENSSL_SOURCE_PATH
    REPO openssl/openssl
    REF a92271e03a8d0dee507b6f1e7f49512568b2c7ad
    SHA512 fb6c078f6cbe629c704bae7eac621a4917d6c5a6fc6cda8e95dcd852e69437747c27ad13a1514a304cdf10904b880efecee2808ad728f205931ee9b11ec6426b
    HEAD_REF master
)

# pybind11 submodule
vcpkg_from_github(
    OUT_SOURCE_PATH PYBIND11_SOURCE_PATH
    REPO pybind/pybind11
    REF 0bd8896a4010f2d91b2340570c24fa08606ec406
    SHA512 b30d5663e7cdf176e28b35a7aa70f617f748b4e2faacb90d6d914653500aca577eba0440273567dc24409ca5858f851374e7f1a2bae62bf42dfeceaf7f8f06e5
    HEAD_REF master
)

# vowpal wabbit submodule
vcpkg_from_github(
    OUT_SOURCE_PATH VW_SOURCE_PATH
    REPO VowpalWabbit/vowpal_wabbit
    REF 4256bf10d7faa25e30fdb489abc9340ab366d53f
    SHA512 7577137b1dc455b014b0b4372d3b6ba3b8c77eadf84324fc88d32d0979c487bc9285c0411d754459a635d0f7aee4a9f97de1e07c4ec2d1ae7086606510eca84c
    HEAD_REF master
)

# zstd zstd submodule
vcpkg_from_github(
    OUT_SOURCE_PATH ZSTD_SOURCE_PATH
    REPO facebook/zstd
    REF b706286adbba780006a47ef92df0ad7a785666b6
    SHA512 1be43e8cc1dad9dd59036f86a7dd579b8fcbf16b3ebae62f38aa0397f45ab0eab2e97e924cede40428fa9125a2e5e567694bb04a0c9ec0c4275a79cd2ef8eb11
    HEAD_REF master
)

##########################################################################################################################################
## get vowpal wabbit dependencies

# boost math submodule
vcpkg_from_github(
    OUT_SOURCE_PATH BOOSTMATH_SOURCE_PATH
    REPO boostorg/math
    REF ed01dae24893bb69c02c6d599acc74bdb8f46bda
    SHA512 d4726314c869cad4b70986eb607a274d0cd5bc276a0c69d083b28e02f656c8a55d66753fe9df2106a86540e63e87df6bedeb40701c962d85b43d851732b4889a
    HEAD_REF master
)

# ensmallen submodule
vcpkg_from_github(
    OUT_SOURCE_PATH ENSMALLEN_SOURCE_PATH
    REPO mlpack/ensmallen
    REF 27246082ac20493d2ee2ce834537afac973ecef3
    SHA512 a8f197f00897540050e099d040c51e5ee0c676404ad697b189442cb3aed5e385c6804293f4ed12ca08b3e62e6ec1297b0508a441eacde335d42e085adfcd3c9c
    HEAD_REF master
)

# fmt submodule
vcpkg_from_github(
    OUT_SOURCE_PATH FMT_SOURCE_PATH
    REPO fmtlib/fmt
    REF a33701196adfad74917046096bf5a2aa0ab0bb50
    SHA512 0faf00e99b332fcb3d9fc50cc9649ddc004ca9035f3652c1a001facee725dab09f67b65a9dfcce0aedb47e76c74c45a9262a1fd6e250a9e9a27c7d021c8ee6b8
    HEAD_REF master
)

# rapidjson submodule
vcpkg_from_github(
    OUT_SOURCE_PATH RAPIDJSON_SOURCE_PATH
    REPO Tencent/rapidjson
    REF f54b0e47a08782a6131cc3d60f94d038fa6e0a51
    SHA512 f30796721c0bfc789d91622b3af6db8d4fb4947a6da3fcdd33e8f37449a28e91dbfb23a98749272a478ca991aaf1696ab159c53b50f48ef69a6f6a51a7076d01
    HEAD_REF master
)

# spdlog submodule
vcpkg_from_github(
    OUT_SOURCE_PATH SPDLOG_SOURCE_PATH
    REPO gabime/spdlog
    REF ad0e89cbfb4d0c1ce4d097e134eb7be67baebb36
    SHA512 507427825bcfe530a613d3b33dd003b64507fed6655ec4c5004d86565781f9fe96f789296154cb37b1316e0467013a1c30d6b8ffb953aef02ad62754633da3fe
    HEAD_REF master
)

# spdlog submodule
vcpkg_from_github(
    OUT_SOURCE_PATH ZLIB_SOURCE_PATH
    REPO madler/zlib
    REF 04f42ceca40f73e2978b50e93806c2a18c1281fc
    SHA512 accafce8ad6ad1d8706f38bda15a7ffcbf4b534380bcde50fc09b3b6d1aa8dfbab84c50fe861a2ac0892643792e4708bfd8438feec7e876d98b028ba5f2ca919
    HEAD_REF master
)

vcpkg_from_gitlab(
    GITLAB_URL https://gitlab.com
    OUT_SOURCE_PATH EIGEN_SOURCE_PATH
    REPO libeigen/eigen
    REF 8b4efc8ed8a65415e248d54fbc9afdd964c94f64
    SHA512 b05058b97c2a6f0ae11e93fc9739169de16cdd7119efb88454b039b42d30118967a62571ebed005d1799e0d97c8548a334b37e2c49279aebc9373169e88863c8
    HEAD_REF master
)

vcpkg_from_gitlab(
    GITLAB_URL https://gitlab.com
    OUT_SOURCE_PATH ARMIDILLO_SOURCE_PATH
    REPO conradsnicta/armadillo-code
    REF ef95c25d2f2d6170a9cb0e90558bb100144ec4df
    SHA512 e8c703acdd112d12e588415cd7600ff6907ff3c9f6ad700090676e8acd7a5c0440158eb777a1a1e80471d84fd2e9b7eb2e9adb08ba85bb3fd5272ea6c35403c1
    HEAD_REF 14.0.x
)

##########################################################################################################################################
## shared dependencies of rl and vw

# vcpkg submodule
vcpkg_from_github(
    OUT_SOURCE_PATH VCPKG_SOURCE_PATH
    REPO microsoft/vcpkg
    REF 53bef8994c541b6561884a8395ea35715ece75db
    SHA512 014c8c6c504b8f8170437f4e1f5f1e78aa62f8dc35da946ea5d4de857cc7600703676ab01645fc404eb14c01dddab1aab34a84a35d9cc301cc43a85df70889dc
    HEAD_REF master
)

##########################################################################################################################################
## copy the subs to the source path

# reinforcement_learning ext_libs
file(COPY "${CCPRESTSDK_SOURCE_PATH}/" DESTINATION "${SOURCE_PATH}/ext_libs/cpprestsdk")
file(COPY "${OPENSSL_SOURCE_PATH}/" DESTINATION "${SOURCE_PATH}/ext_libs/openssl")
file(COPY "${PYBIND11_SOURCE_PATH}/" DESTINATION "${SOURCE_PATH}/ext_libs/pybind11")
file(COPY "${VCPKG_SOURCE_PATH}/" DESTINATION "${SOURCE_PATH}/ext_libs/vcpkg")
file(COPY "${VW_SOURCE_PATH}/" DESTINATION "${SOURCE_PATH}/ext_libs/vowpal_wabbit")
file(COPY "${ZSTD_SOURCE_PATH}/" DESTINATION "${SOURCE_PATH}/ext_libs/zstd")

# vw ext_libs
file(COPY "${BOOSTMATH_SOURCE_PATH}/" DESTINATION "${SOURCE_PATH}/ext_libs/vowpal_wabbit/ext_libs/boost_math")
file(COPY "${ENSMALLEN_SOURCE_PATH}/" DESTINATION "${SOURCE_PATH}/ext_libs/vowpal_wabbit/ext_libs/ensmallen")
file(COPY "${FMT_SOURCE_PATH}/" DESTINATION "${SOURCE_PATH}/ext_libs/vowpal_wabbit/ext_libs/fmt")
file(COPY "${RAPIDJSON_SOURCE_PATH}/" DESTINATION "${SOURCE_PATH}/ext_libs/vowpal_wabbit/ext_libs/rapidjson")
file(COPY "${SPDLOG_SOURCE_PATH}/" DESTINATION "${SOURCE_PATH}/ext_libs/vowpal_wabbit/ext_libs/spdlog")
file(COPY "${ZLIB_SOURCE_PATH}/" DESTINATION "${SOURCE_PATH}/ext_libs/vowpal_wabbit/ext_libs/zlib")
file(COPY "${VCPKG_SOURCE_PATH}/" DESTINATION "${SOURCE_PATH}/ext_libs/vowpal_wabbit/ext_libs/vcpkg")
file(COPY "${EIGEN_SOURCE_PATH}/" DESTINATION "${SOURCE_PATH}/ext_libs/vowpal_wabbit/ext_libs/eigen")
file(COPY "${ARMIDILLO_SOURCE_PATH}/" DESTINATION "${SOURCE_PATH}/ext_libs/vowpal_wabbit/ext_libs/armadillo-code")

##########################################################################################################################################
## some cleanup

file(REMOVE_RECURSE "${CCPRESTSDK_SOURCE_PATH}")
file(REMOVE_RECURSE "${OPENSSL_SOURCE_PATH}")
file(REMOVE_RECURSE "${PYBIND11_SOURCE_PATH}")
file(REMOVE_RECURSE "${VCPKG_SOURCE_PATH}")
file(REMOVE_RECURSE "${VW_SOURCE_PATH}")
file(REMOVE_RECURSE "${ZSTD_SOURCE_PATH}")
file(REMOVE_RECURSE "${BOOSTMATH_SOURCE_PATH}")
file(REMOVE_RECURSE "${ENSMALLEN_SOURCE_PATH}")
file(REMOVE_RECURSE "${FMT_SOURCE_PATH}")
file(REMOVE_RECURSE "${RAPIDJSON_SOURCE_PATH}")
file(REMOVE_RECURSE "${SPDLOG_SOURCE_PATH}")
file(REMOVE_RECURSE "${ZLIB_SOURCE_PATH}")
file(REMOVE_RECURSE "${VCPKG_SOURCE_PATH}")
file(REMOVE_RECURSE "${EIGEN_SOURCE_PATH}")
file(REMOVE_RECURSE "${ARMIDILLO_SOURCE_PATH}")

##########################################################################################################################################
## setup build options
set(BUILDTREE_PATH "${CURRENT_BUILDTREES_DIR}/${TARGET_TRIPLET}-rel")

set(AZURE_AUTH_ENABLED "OFF" CACHE BOOL "Disable Azure authentication")
if ("azure_auth" IN_LIST FEATURES)
   set(AZURE_AUTH_ENABLED "ON" CACHE BOOL "Enable Azure authentication" FORCE)
endif()

set(EXTERNAL_PARSER_ENABLED "OFF" CACHE BOOL "Disable generating the external parser")
if ("external-parser" IN_LIST FEATURES)
   set(EXTERNAL_PARSER_ENABLED "ON" CACHE BOOL "Disable generating the external parser" FORCE)
endif()

set(FINAL_BUILD_OPTIONS
   -Drlclientlib_BUILD_DOTNET=OFF 
   -DBUILD_FLATBUFFERS=OFF
   -DWARNING_AS_ERROR=OFF
   -DRAPIDJSON_SYS_DEP=ON
   -DBUILD_JAVA=OFF
   -DBUILD_PYTHON=OFF
   -DBUILD_TESTING=OFF
   -DRL_BUILD_EXTERNAL_PARSER=${EXTERNAL_PARSER_ENABLED}
   -DBUILD_EXPERIMENTAL_BINDING=OFF
   -DRL_LINK_AZURE_LIBS=${AZURE_AUTH_ENABLED}
   -DRL_STATIC_DEPS=ON
   -DRL_OPENSSL_SYS_DEP=ON
   -DRL_USE_ZSTD=ON
   -Dvw_USE_AZURE_FACTORIES=ON
)

set(FINAL_DEBUG_OPTIONS "")
set(FINAL_RELEASE_OPTIONS "")

if(VCPKG_TARGET_IS_WINDOWS)
   list(APPEND FINAL_BUILD_OPTIONS "-DVCPKG_TARGET_TRIPLET=x64-windows-static")
   list(APPEND FINAL_BUILD_OPTIONS "-DVCPKG_HOST_TRIPLET=x64-windows-static")
   list(APPEND FINAL_DEBUG_OPTIONS "-DCMAKE_MSVC_RUNTIME_LIBRARY=MultiThreadedDebug")
   list(APPEND FINAL_RELEASE_OPTIONS "-DCMAKE_MSVC_RUNTIME_LIBRARY=MultiThreaded")
endif()

##########################################################################################################################################
## configure

vcpkg_cmake_configure(
   SOURCE_PATH "${SOURCE_PATH}"
   OPTIONS
      ${FINAL_BUILD_OPTIONS}
   OPTIONS_DEBUG
      ${FINAL_DEBUG_OPTIONS}
   OPTIONS_RELEASE
      ${FINAL_RELEASE_OPTIONS}
)

vcpkg_cmake_install()

# install usage and license files
file(INSTALL "${CMAKE_CURRENT_LIST_DIR}/usage" DESTINATION "${CURRENT_PACKAGES_DIR}/share/${PORT}")
vcpkg_install_copyright(FILE_LIST "${SOURCE_PATH}/LICENSE.txt")

# move tools
vcpkg_copy_tools(TOOL_NAMES vw SEARCH_DIR "${BUILDTREE_PATH}/external_parser")
vcpkg_copy_tools(TOOL_NAMES rl_sim_cpp.out SEARCH_DIR "${BUILDTREE_PATH}/examples/rl_sim_cpp")

# cleanup debug includes
if (EXISTS "${CURRENT_PACKAGES_DIR}/debug/include")
    file(REMOVE_RECURSE "${CURRENT_PACKAGES_DIR}/debug/include")
endif()

vcpkg_fixup_pkgconfig()
