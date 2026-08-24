"use strict";
var __decorate = (this && this.__decorate) || function (decorators, target, key, desc) {
    var c = arguments.length, r = c < 3 ? target : desc === null ? desc = Object.getOwnPropertyDescriptor(target, key) : desc, d;
    if (typeof Reflect === "object" && typeof Reflect.decorate === "function") r = Reflect.decorate(decorators, target, key, desc);
    else for (var i = decorators.length - 1; i >= 0; i--) if (d = decorators[i]) r = (c < 3 ? d(r) : c > 3 ? d(target, key, r) : d(target, key)) || r;
    return c > 3 && r && Object.defineProperty(target, key, r), r;
};
Object.defineProperty(exports, "__esModule", { value: true });
exports.LandmarkModule = void 0;
const common_1 = require("@nestjs/common");
const landmark_service_1 = require("./landmark.service");
let LandmarkModule = class LandmarkModule {
};
exports.LandmarkModule = LandmarkModule;
exports.LandmarkModule = LandmarkModule = __decorate([
    (0, common_1.Module)({
        providers: [landmark_service_1.LandmarkService],
        exports: [landmark_service_1.LandmarkService],
    })
], LandmarkModule);
//# sourceMappingURL=landmark.module.js.map