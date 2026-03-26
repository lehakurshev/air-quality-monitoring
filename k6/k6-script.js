import http from 'k6/http'
import { check, sleep } from 'k6'
import papaparse from 'https://jslib.k6.io/papaparse/5.1.1/index.js'
import { SharedArray } from 'k6/data'

// Загружаем и парсим CSV файл
const csvData = new SharedArray('coordinates', function() {
  const data = open('./metair_metadata_eea.csv')
  return papaparse.parse(data, { header: true, skipEmptyLines: true }).data
})

// Извлекаем уникальные координаты (только первые 100)
const uniqueCoordinates = (() => {
  const coordsSet = new Set()
  const uniqueCoords = []
  
  csvData.forEach(row => {
    // Останавливаемся после нахождения 100 уникальных координат
    if (uniqueCoords.length >= 100) return
    
    const lat = parseFloat(row.latitude_metair)
    const lon = parseFloat(row.longitude_metair)
    
    // Проверяем, что координаты валидны
    if (!isNaN(lat) && !isNaN(lon)) {
      const key = `${lat},${lon}`
      if (!coordsSet.has(key)) {
        coordsSet.add(key)
        uniqueCoords.push({ latitude: lat, longitude: lon })
      }
    }
  })
  
  console.log(`Found ${uniqueCoords.length} unique coordinates (limited to 100)`)
  return uniqueCoords
})()

export const options = {
  scenarios: {
    register: {
      executor: "constant-vus",
      vus: 100,
      duration: "2m",
      exec: "registerUsers"
    },
    load: {
      executor: "ramping-vus",
      startTime: "2m",
      startVUs: 0,
      stages: [
        { duration: "2m", target: uniqueCoordinates.length }, // Используем количество уникальных координат (до 100)
        { duration: "58m", target: uniqueCoordinates.length }
      ],
      exec: "loadTest"
    }
  }
}

const BASE_URL = 'http://backend:8080'

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

// Хранилище для назначенных координат каждому VU
const vuCoordinates = new Map()

export function loadTest() {
  // Получаем или назначаем координаты для текущего VU
  let coordinates = vuCoordinates.get(__VU)
  
  if (!coordinates) {
    // Назначаем координаты на основе индекса VU
    const coordIndex = (__VU - 1) % uniqueCoordinates.length
    coordinates = uniqueCoordinates[coordIndex]
    vuCoordinates.set(__VU, coordinates)
    console.log(`VU ${__VU} assigned coordinates: ${coordinates.latitude}, ${coordinates.longitude}`)
  }

  const email = randomString(20)
  const password = randomString(20)

  const registerRes = http.post(
    `${BASE_URL}/api/auth/register`,
    JSON.stringify({ email, password }),
    { headers: { 'Content-Type': 'application/json' } }
  )

  if (registerRes.status !== 200) return

  const apiToken = registerRes.json().apiToken

  // Используем назначенные координаты
  const latitude = coordinates.latitude
  const longitude = coordinates.longitude

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