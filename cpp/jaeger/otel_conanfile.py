from conan import ConanFile
from conan.tools.cmake import CMakeToolchain, CMake, cmake_layout, CMakeDeps
from conan.tools.files import get, copy
import os

class OtelMicroserviceConan(ConanFile):
    name = "otel-microservice"
    version = "1.0.0"
    
    # Binary configuration
    settings = "os", "compiler", "build_type", "arch"
    
    # Sources
    exports_sources = "CMakeLists.txt", "main.cpp"
    
    def requirements(self):
        self.requires("grpc/1.48.0")
        self.requires("protobuf/3.21.4")
        self.requires("libcurl/7.86.0")
        self.requires("openssl/1.1.1s")
    
    def build_requirements(self):
        self.tool_requires("cmake/3.25.0")
    
    def layout(self):
        cmake_layout(self)
    
    def generate(self):
        deps = CMakeDeps(self)
        deps.generate()
        tc = CMakeToolchain(self)
        tc.generate()
        
    def build(self):
        # First, build OpenTelemetry C++
        self.output.info("Building OpenTelemetry C++...")
        
        otel_source = os.path.join(self.build_folder, "opentelemetry-cpp")
        
        # Clone if not exists
        if not os.path.exists(otel_source):
            get(self, 
                "https://github.com/open-telemetry/opentelemetry-cpp/archive/refs/tags/v1.14.2.tar.gz",
                destination=self.build_folder,
                strip_root=True)
            os.rename(os.path.join(self.build_folder, "opentelemetry-cpp-1.14.2"), otel_source)
        
        # Build OpenTelemetry
        otel_cmake = CMake(self)
        otel_build = os.path.join(otel_source, "build")
        os.makedirs(otel_build, exist_ok=True)
        
        # Configure OpenTelemetry
        self.run(f"cd {otel_source} && "
                f"cmake -B build -S . "
                f"-DCMAKE_BUILD_TYPE=Release "
                f"-DCMAKE_INSTALL_PREFIX={self.build_folder}/otel_install "
                f"-DBUILD_TESTING=OFF "
                f"-DWITH_OTLP_GRPC=ON "
                f"-DWITH_OTLP_HTTP=ON "
                f"-DWITH_ZIPKIN=OFF "
                f"-DWITH_JAEGER=OFF "
                f"-DWITH_PROMETHEUS=OFF")
        
        # Build and install OpenTelemetry
        self.run(f"cmake --build {otel_build} -j")
        self.run(f"cmake --install {otel_build}")
        
        # Now build the service
        cmake = CMake(self)
        cmake.configure()
        cmake.build()
    
    def package(self):
        copy(self, "service", src=self.build_folder, dst=os.path.join(self.package_folder, "bin"))
        
    def package_info(self):
        self.cpp_info.bindirs = ["bin"]