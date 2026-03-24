import http from 'k6/http'
import { check, sleep } from 'k6'


export const options = {
  scenarios: {

    register: {
      executor: "constant-vus",
      vus: 200,
      duration: "2m",
      exec: "registerUsers"
    },

    load: {
      executor: "ramping-vus",
      startTime: "2m",
      startVUs: 0,
      stages: [
        { duration: "2m", target: 1000 },
        { duration: "58m", target: 1000 }
      ],
      exec: "loadTest"
    }
  }
}

const BASE_URL = 'http://backend:8080'

const BASE_LAT = 56.8333
const BASE_LON = 60.5833
const OFFSET = 0.05

function randomString(length) {
  const chars = 'abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789'
  let result = ''
  for (let i = 0; i < length; i++) {
    result += chars.charAt(Math.floor(Math.random() * chars.length))
  }
  return result
}

export function registerUsers() {
  const email = randomString(20)
  const password = randomString(20)

  const res = http.post(
    `${BASE_URL}/api/auth/register`,
    JSON.stringify({ email, password }),
    { headers: { 'Content-Type': 'application/json' } }
  )

  check(res, {
    'register ok': (r) => r.status === 200
  })
}

export function loadTest() {

  const email = randomString(20)
  const password = randomString(20)

  const registerRes = http.post(
    `${BASE_URL}/api/auth/register`,
    JSON.stringify({ email, password }),
    { headers: { 'Content-Type': 'application/json' } }
  )

  if (registerRes.status !== 200) return

  const apiToken = registerRes.json().apiToken

  const latitude = BASE_LAT + (Math.random() * 2 - 1) * OFFSET
  const longitude = BASE_LON + (Math.random() * 2 - 1) * OFFSET

  for (let tokenCycle = 0; tokenCycle < 2; tokenCycle++) {

    const tokenRes = http.post(
      `${BASE_URL}/api/auth/token`,
      JSON.stringify({ apiToken }),
      { headers: { 'Content-Type': 'application/json' } }
    )

    if (tokenRes.status !== 200) return

    const accessToken = tokenRes.json().accessToken

    const headers = {
      headers: {
        'Content-Type': 'application/json',
        Authorization: `Bearer ${accessToken}`
      }
    }

    for (let i = 0; i < 30; i++) {

      const payload = JSON.stringify({
        co: Math.random(),
        no2: Math.random(),
        pm25: Math.random() * 50,
        pm10: Math.random() * 50,
        latitude,
        longitude
      })

      const res = http.post(
        `${BASE_URL}/api/measurement`,
        payload,
        headers
      )

      check(res, {
        'measurement ok': (r) => r.status === 200
      })

      sleep(60)
    }

    sleep(1800)
  }
}