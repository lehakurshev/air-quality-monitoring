import http from 'k6/http'
import { check, sleep } from 'k6'
import papaparse from 'https://jslib.k6.io/papaparse/5.1.1/index.js'
import { SharedArray } from 'k6/data'

const BASE_URL = 'http://backend:8080'
const TARGET_COORDINATES = 1000

// =====================================
// CSV parsing (runs once)
// =====================================
const finalCoordinates = new SharedArray('coordinates', () => {
  const csv = open('./metair_metadata_eea.csv')
  const rows = papaparse.parse(csv, {
    header: true,
    skipEmptyLines: true,
  }).data

  const uniqueSet = new Set()
  const uniqueCoords = []

  for (const row of rows) {
    const lat = Number(row.latitude_metair)
    const lon = Number(row.longitude_metair)

    if (!Number.isFinite(lat) || !Number.isFinite(lon)) continue

    const key = `${lat},${lon}`
    if (uniqueSet.has(key)) continue

    uniqueSet.add(key)
    uniqueCoords.push({
      latitude: lat,
      longitude: lon,
    })
  }

  // Fisher-Yates shuffle
  for (let i = uniqueCoords.length - 1; i > 0; i--) {
    const j = Math.floor(Math.random() * (i + 1))
    ;[uniqueCoords[i], uniqueCoords[j]] = [uniqueCoords[j], uniqueCoords[i]]
  }

  return uniqueCoords.slice(0, Math.min(TARGET_COORDINATES, uniqueCoords.length))
})

// =====================================
// Test options
// =====================================
export const options = {
  discardResponseBodies: true,

  scenarios: {
    load: {
      executor: 'ramping-vus',
      startVUs: 0,
      stages: [
        { duration: '3m', target: finalCoordinates.length }, // плавнее разгон
        { duration: '57m', target: finalCoordinates.length },
      ],
      exec: 'loadTest',
    },
  },
}

// =====================================
// Helpers
// =====================================
function randomString(length) {
  const chars = 'abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789'
  let result = ''
  for (let i = 0; i < length; i++) {
    result += chars[(Math.random() * chars.length) | 0]
  }
  return result
}

// =====================================
// Per-VU state
// =====================================
let vuState = null

function initVuState() {
  if (vuState) return vuState

  const coordIndex = (__VU - 1) % finalCoordinates.length
  const coordinates = finalCoordinates[coordIndex]

  const email = `${randomString(12)}_${__VU}@test.local`
  const password = randomString(20)

  const registerRes = http.post(
    `${BASE_URL}/api/auth/register`,
    JSON.stringify({ email, password }),
    {
      headers: { 'Content-Type': 'application/json' },
      responseType: 'text',
    }
  )

  check(registerRes, {
    'register ok': (r) => r.status === 200,
  })

  if (registerRes.status !== 200) {
    throw new Error(`VU ${__VU}: registration failed`)
  }

  vuState = {
    apiToken: registerRes.json().apiToken,
    latitude: coordinates.latitude,
    longitude: coordinates.longitude,
  }

  return vuState
}

// =====================================
// Main load scenario
// =====================================
export function loadTest() {
  const state = initVuState()

  for (let tokenCycle = 0; tokenCycle < 2; tokenCycle++) {
    const tokenRes = http.post(
      `${BASE_URL}/api/auth/token`,
      JSON.stringify({ apiToken: state.apiToken }),
      {
        headers: { 'Content-Type': 'application/json' },
        responseType: 'text',
      }
    )

    check(tokenRes, {
      'token ok': (r) => r.status === 200,
    })

    if (tokenRes.status !== 200) return

    const accessToken = tokenRes.json().accessToken

    const params = {
      headers: {
        'Content-Type': 'application/json',
        Authorization: `Bearer ${accessToken}`,
      },
      responseType: 'none', // very important for RAM
    }

    for (let i = 0; i < 30; i++) {
      const payload = JSON.stringify({
        co: Math.random(),
        no2: Math.random(),
        pm25: Math.random() * 50,
        pm10: Math.random() * 50,
        latitude: state.latitude,
        longitude: state.longitude,
      })

      const res = http.post(
        `${BASE_URL}/api/measurement`,
        payload,
        params
      )

      check(res, {
        'measurement ok': (r) => r.status === 200,
      })

      sleep(60)
    }

    // refresh token every 30 mins
    sleep(1800)
  }
}