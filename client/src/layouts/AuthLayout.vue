<template>
  <q-layout view="hHh lpR fFf">
    <q-header class="bg-transparent">
      <q-toolbar>
        <q-space />
        <LanguageSwitch class="text-grey-8" />
      </q-toolbar>
    </q-header>

    <q-page-container>
      <router-view />
    </q-page-container>
  </q-layout>
</template>

<script setup lang="ts">
import LanguageSwitch from '@/components/LanguageSwitch.vue';
</script>
