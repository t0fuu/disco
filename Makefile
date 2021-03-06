OPTSS="parallel-jobs=4"
BUILD=mono packages/build/FAKE/tools/FAKE.exe build.fsx

CURRENT_DIR:=$(shell dirname $(realpath $(lastword $(MAKEFILE_LIST))))
SCRIPT_DIR=$(CURRENT_DIR)/src/Scripts
SHELL_NIX=$(SCRIPT_DIR)/Nix/shell.nix

MONO_THREADS_PER_CPU := 100
FRONTEND_IP := localhost
FRONTEND_PORT := 7000

export MONO_THREADS_PER_CPU
export FRONTEND_IP
export FRONTEND_PORT

#              _   _
#  _ __   __ _| |_(_)_   _____
# | '_ \ / _` | __| \ \ / / _ \
# | | | | (_| | |_| |\ V /  __/
# |_| |_|\__,_|\__|_| \_/ \___|

run.tests:
	@nix-shell $(SHELL_NIX) -A discoEnv --run "$(BUILD) RunTestsFast $(OPTS)"

tests:
	${BUILD} BuildTests ${OPTS}

build: paket.restore zeroconf
	${BUILD} ${OPTS}

service:
	${BUILD} BuildDebugService ${OPTS}

service.release:
	${BUILD} BuildReleaseService ${OPTS}

core:
	${BUILD} BuildDebugCore ${OPTS}

core.release:
	${BUILD} BuildReleaseCore ${OPTS}

nodes:
	${BUILD} BuildDebugNodes ${OPTS}

nodes.release:
	${BUILD} BuildReleaseNodes ${OPTS}

serialization:
	${BUILD} GenerateSerialization ${OPTS}

zeroconf:
	${BUILD} BuildDebugZeroconf ${OPTS}

sdk:
	${BUILD} BuildDebugSdk ${OPTS}

sdk.release:
	${BUILD} BuildReleaseSdk ${OPTS}

client:
	${BUILD} BuildDebugMockClient ${OPTS}

raspi:
	${BUILD} BuildDebugRaspi ${OPTS}

#  _ __ _   _ _ __
# | '__| | | | '_ \
# | |  | |_| | | | |
# |_|   \__,_|_| |_|

run.client:
	@nix-shell $(SHELL_NIX) -A discoEnv --run "mono src/Disco/bin/Debug/MockClient/client.exe -n MOCK-$(hostname) -h ${HOST} -p ${PORT} -b ${BIND}"

run.frontend:
	@nix-shell $(SHELL_NIX) -A discoEnv --run "npm start"

run.service:
	@nix-shell $(SHELL_NIX) -A discoEnv --run "mono src/Disco/bin/${TARGET}/Disco/disco.exe start --bind=${FRONTEND_IP}"

run.service.1:
	@nix-shell $(SHELL_NIX) -A discoEnv --run "mono src/Disco/bin/${TARGET}/Disco/disco.exe start --machine=${HOME}/disco/machines/one"

run.service.2:
	@nix-shell $(SHELL_NIX) -A discoEnv --run "mono src/Disco/bin/${TARGET}/Disco/disco.exe start --machine=${HOME}/disco/machines/two"

run.service.3:
	@nix-shell $(SHELL_NIX) -A discoEnv --run "mono src/Disco/bin/${TARGET}/Disco/disco.exe start --machine=${HOME}/disco/machines/three"

run.service.1.project:
	@nix-shell $(SHELL_NIX) -A discoEnv --run "mono src/Disco/bin/${TARGET}/Disco/disco.exe start --machine=${HOME}/disco/machines/one --project=${PROJECT}"

run.service.2.project:
	@nix-shell $(SHELL_NIX) -A discoEnv --run "mono src/Disco/bin/${TARGET}/Disco/disco.exe start --machine=${HOME}/disco/machines/two --project=${PROJECT}"

run.service.3.project:
	@nix-shell $(SHELL_NIX) -A discoEnv --run "mono src/Disco/bin/${TARGET}/Disco/disco.exe start --machine=${HOME}/disco/machines/three --project=${PROJECT}"

run.web.tests:
	@nix-shell $(SHELL_NIX) -A discoEnv --run "$(BUILD) RunWebTestsFast $(OPTS)"

run.service.1.project.profile:
	@nix-shell $(SHELL_NIX) -A discoEnv --run "mono --profile=log:sample,noalloc src/Disco/bin/${TARGET}/Disco/disco.exe start --machine=${HOME}/disco/machines/one --project=${PROJECT}"

#   __                 _                 _
#  / _|_ __ ___  _ __ | |_ ___ _ __   __| |
# | |_| '__/ _ \| '_ \| __/ _ \ '_ \ / _` |
# |  _| | | (_) | | | | ||  __/ | | | (_| |
# |_| |_|  \___/|_| |_|\__\___|_| |_|\__,_|

css:
	@nix-shell $(SHELL_NIX) -A discoEnv --run "$(BUILD) BuildCss $(OPTS)"

frontend:
	@nix-shell $(SHELL_NIX) -A discoEnv --run "$(BUILD) BuildFrontendFast $(OPTS)"

frontend.plugins:
	@nix-shell $(SHELL_NIX) -A discoEnv --run "$(BUILD) BuildFrontendPlugins $(OPTS)"

frontend.full:
	@nix-shell $(SHELL_NIX) -A discoEnv --run "$(BUILD) BuildFrontend $(OPTS)"

web.tests:
	@nix-shell $(SHELL_NIX) -A discoEnv --run "$(BUILD) BuildWebTestsFast $(OPTS)"

#      _
#   __| | ___   ___ ___
#  / _` |/ _ \ / __/ __|
# | (_| | (_) | (__\__ \
#  \__,_|\___/ \___|___/

docs:
	${BUILD} DebugDocs ${OPTS}

#        _ _
#   __ _| | |
#  / _` | | |
# | (_| | | |
#  \__,_|_|_|

tests.all:
	@nix-shell $(SHELL_NIX) -A discoEnv --run "$(BUILD) AllTests $(OPTS)"

debug.all:
	${BUILD} DebugAll ${OPTS}

clean:
	@git clean -fdX

#           _
#  _ __ ___| | ___  __ _ ___  ___
# | '__/ _ \ |/ _ \/ _` / __|/ _ \
# | | |  __/ |  __/ (_| \__ \  __/
# |_|  \___|_|\___|\__,_|___/\___|

release: restore
	${BUILD} Release ${OPTS}

#      _          _ _
#  ___| |__   ___| | |
# / __| '_ \ / _ \ | |
# \__ \ | | |  __/ | |
# |___/_| |_|\___|_|_|

shell:
	@nix-shell $(SHELL_NIX) -A discoEnv

nixfsi:
	@nix-shell $(SHELL_NIX) -A discoEnv --run "fsi --use:.paket/load/main.group.fsx --use:$(SCRIPT_DIR)/Fsx/Disco.Core.fsx"

#  ____             _
# |  _ \  ___   ___| | _____ _ __
# | | | |/ _ \ / __| |/ / _ \ '__|
# | |_| | (_) | (__|   <  __/ |
# |____/ \___/ \___|_|\_\___|_|

docker:
	${BUILD} DebugDocker ${OPTS}

image_base:
	@docker build \
		--label disco \
		--tag disco:base \
		${CURRENT_DIR}/src/Disco/Dockerbase/

image: docker
	@docker build \
		--label disco \
		--tag disco:$(shell git log -n1 --oneline | cut -d\  -f1) \
		${CURRENT_DIR}/src/Disco/bin/Debug/Disco/

create:
	@mkdir -p ${PROJECT}
	@docker run -i --rm --net=host \
		-v ${PROJECT}:/project \
		-e DISCO_BIND=127.0.0.1 \
		-e DISCO_NODE_ID=${DISCO_NODE_ID} \
		-e DISCO_GIT_PORT=${DISCO_GIT} \
		-e DISCO_WEB_PORT=${DISCO_WEB} \
		-e DISCO_WS_PORT=${DISCO_WS} \
		-e DISCO_RAFT_PORT=${DISCO_RAFT} \
		-e DISCO_NAME=${DISCO_NAME} \
		-e COMMAND=create \
		${IMAGE}

docker.shell:
	@docker run -p 7000:7000 -i --rm \
		-v ${PROJECT}:/project \
		-e DISCO_NODE_ID=${DISCO_NODE_ID} \
		-e COMMAND=shell \
		${IMAGE}
start:
	@docker run -p 7000:7000 -i --rm \
		-v ${PROJECT}:/project \
		-e DISCO_NODE_ID=${DISCO_NODE_ID} \
		-e COMMAND=start \
		${IMAGE}

start.interactive:
	@docker run -i --rm --net=host \
		-v ${PROJECT}:/project \
		-e DISCO_NODE_ID=${DISCO_NODE_ID} \
		-e COMMAND=interactive \
		${IMAGE}

start.noweb:
	@docker run -i --rm --net=host \
		-v ${PROJECT}:/project \
		-e DISCO_NOWEB=true \
		-e DISCO_NODE_ID=${DISCO_NODE_ID} \
		-e COMMAND=start \
		${IMAGE}

enter:
	@docker run -i --rm --net=host \
		-v ${PROJECT}:/project \
		-e DISCO_NODE_ID=${DISCO_NODE_ID} \
		-e COMMAND=shell \
		${IMAGE}

#              _        _
#  _ __   __ _| | _____| |_
# | '_ \ / _` | |/ / _ \ __|
# | |_) | (_| |   <  __/ |_
# | .__/ \__,_|_|\_\___|\__|
# |_|

restore: paket.restore paket.generate
	@nix-shell $(SHELL_NIX) -A discoEnv --run "$(BUILD) Bootstrap $(OPTS)"

paket.generate:
	@mono .paket/paket.exe generate-load-scripts --type fsx

paket.restore:
	@mono .paket/paket.exe restore

paket.update:
	@mono .paket/paket.exe update

paket.install:
	@mono .paket/paket.exe install
