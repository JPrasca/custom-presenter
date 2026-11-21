<script setup lang="ts">
import StatusBadge from '../../components/StatusBadge.vue';
</script>

<template>
  <main class="operator">
    <header>
      <h1>Custom Presenter</h1>
      <StatusBadge label="Sistema listo" :active="true" />
    </header>
    <p class="subtitle">Panel de control del operador (MVP en construcción).</p>
  </main>
</template>

<style scoped>
.operator {
  padding: 1.5rem;
}

header {
  display: flex;
  align-items: center;
  gap: 1rem;
  margin-bottom: 0.75rem;
}

.subtitle {
  color: var(--text-muted);
}
</style>
