/// <reference types="vite/client" />

interface ImportMetaEnv {
  /** Short git SHA baked into the build (see ui-app Dockerfile + Taskfile.build.yml). */
  readonly VITE_GIT_SHA?: string;
  /** UTC build timestamp baked into the build. */
  readonly VITE_BUILD_TIME?: string;
}

interface ImportMeta {
  readonly env: ImportMetaEnv;
}
