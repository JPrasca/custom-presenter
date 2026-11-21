<script setup lang="ts">
interface Props {
  label: string;
  active?: boolean;
}

withDefaults(defineProps<Props>(), {
  active: false,
});
</script>

<template>
  <span class="badge" :class="{ active }">
    {{ label }}
  </span>
</template>

<style scoped>
.badge {
  display: inline-block;
  padding: 0.25rem 0.6rem;
  border-radius: 9999px;
  font-size: 0.75rem;
  font-weight: 600;
  background-color: var(--bg-secondary);
  color: var(--text-muted);
  border: 1px solid #2a2f3a;
}

.badge.active {
  background-color: var(--accent);
  color: #ffffff;
  border-color: var(--accent);
}
</style>
