<script setup lang="ts">
import OperatorView from './views/operator/OperatorView.vue';
import ProjectorView from './views/projector/ProjectorView.vue';
import ObsOverlayView from './views/obs/ObsOverlayView.vue';

const route = typeof window !== 'undefined' ? window.location.pathname : '/';
</script>

<template>
  <ProjectorView v-if="route === '/projector'" />
  <ObsOverlayView v-else-if="route === '/obs'" />
  <OperatorView v-else />
</template>
